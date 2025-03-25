using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PointCloudRenderer : MonoBehaviour
{
    public int maxChunkSize = 65535;
    public float pointSize = 0.005f;
    public GameObject pointCloudElem;
    public Material pointCloudMaterial;

    public int maxNumElems = 1;

    public WebRTCManager webRTCManager;

    void Start()
    {
        //elems = new List<GameObject>();
        UpdatePointSize();
    }

    void Update()
    {
        if (transform.hasChanged)
        {
            UpdatePointSize();
            transform.hasChanged = false;
        }
    }

    void UpdatePointSize()
    {
        pointCloudMaterial.SetFloat("_PointSize", pointSize * transform.localScale.x);
    }

    public void Render(float[] arrVertices, byte[] arrColors)
    {
        int nPoints, nChunks;
        if (arrVertices == null || arrColors == null)
        {
            nPoints = 0;
            nChunks = 0;
        }
        else
        {
            nPoints = arrVertices.Length / 3;
            nChunks = 1 + nPoints / maxChunkSize;
        }

        nChunks = Mathf.Min(nChunks, maxNumElems);

        if (webRTCManager.networkObjects.Count < nChunks)
            AddElems(nChunks - webRTCManager.networkObjects.Count);
        if (webRTCManager.networkObjects.Count > nChunks)
            RemoveElems(webRTCManager.networkObjects.Count - nChunks);

        int offset = 0;
        for (int i = 0; i < nChunks; i++)
        {
            int nPointsToRender = System.Math.Min(maxChunkSize, nPoints - offset);

            Vector3[] points = new Vector3[nPoints];
            int[] indices = new int[nPoints];
            Color[] colors = new Color[nPoints];

            for (int j = 0; j < nPoints; j++)
            {
                int ptIdx = 3 * j;

                points[j] = new Vector3(arrVertices[ptIdx + 0], arrVertices[ptIdx + 1], -arrVertices[ptIdx + 2]);
                indices[j] = j;
                colors[j] = new Color((float)arrColors[ptIdx + 0] / 256.0f, (float)arrColors[ptIdx + 1] / 256.0f, (float)arrColors[ptIdx + 2] / 256.0f, 1.0f);
            }

            // Thin the point cloud
            List<Vector3> newPoints = new List<Vector3>();
            List<Color> newColors = new List<Color>();

            float voxelSize = 0.006f;

            VoxelDownsample(points.ToList(), colors.ToList(), ref newPoints, ref newColors, voxelSize);
            Debug.Log("Original points: " + points.Length + ", new points: " + newPoints.Count);

            webRTCManager.SendPointCloud(newPoints.ToArray(), newColors.ToArray());

            offset += nPointsToRender;
        }
    }

    void AddElems(int nElems)
    {
        for (int i = 0; i < nElems; i++)
        {
            webRTCManager.SpawnNetworkObject(pointCloudElem, new Vector3(0.0f, 0.0f, 0.0f), Quaternion.identity);
        }
    }

    void RemoveElems(int nElems)
    {
        webRTCManager.DestroyNetworkObjects(nElems);
    }

    public void VoxelDownsample(
        List<Vector3> originalPoints,
        List<Color> originalColors,
        ref List<Vector3> newPoints,
        ref List<Color> newColors,
        float voxelSize,
        int minPointsThreshold = 4,  // Discard voxels with fewer points than this
        float densityFactor = 1.6f   // Increase to keep more points in dense areas
    )
    {
        Dictionary<Vector3Int, List<(Vector3, Color)>> voxelMap = new Dictionary<Vector3Int, List<(Vector3, Color)>>();

        // Step 1: Assign points to voxels
        for (int i = 0; i < originalPoints.Count; i++)
        {
            Vector3 point = originalPoints[i];
            Color color = originalColors[i];

            Vector3Int voxelKey = new Vector3Int(
                Mathf.FloorToInt(point.x / voxelSize),
                Mathf.FloorToInt(point.y / voxelSize),
                Mathf.FloorToInt(point.z / voxelSize)
            );

            if (!voxelMap.ContainsKey(voxelKey))
                voxelMap[voxelKey] = new List<(Vector3, Color)>();

            voxelMap[voxelKey].Add((point, color));
        }

        // Step 2: Adaptive sampling based on density
        foreach (var voxel in voxelMap)
        {
            List<(Vector3, Color)> pointsInVoxel = voxel.Value;
            int count = pointsInVoxel.Count;

            // **Noise Removal: Ignore sparse voxels**
            if (count < minPointsThreshold)
                continue;

            // **Keep more points in dense areas**
            int pointsToKeep = Mathf.CeilToInt(Mathf.Sqrt(count) * densityFactor);
            pointsToKeep = Mathf.Min(pointsToKeep, count); // Never exceed the available points

            // Sort points by distance to the voxel center (optional but helps structure)
            Vector3 voxelCenter = new Vector3(
                (voxel.Key.x + 0.5f) * voxelSize,
                (voxel.Key.y + 0.5f) * voxelSize,
                (voxel.Key.z + 0.5f) * voxelSize
            );

            pointsInVoxel.Sort((a, b) =>
                Vector3.Distance(a.Item1, voxelCenter).CompareTo(Vector3.Distance(b.Item1, voxelCenter))
            );

            // **Sample points while prioritizing structure**
            for (int i = 0; i < pointsToKeep; i++)
            {
                newPoints.Add(pointsInVoxel[i].Item1);
                newColors.Add(pointsInVoxel[i].Item2);
            }
        }
    }
}