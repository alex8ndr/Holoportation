using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ElemRenderer : MonoBehaviour
{
    private WebRTCManager webRTCManager;
    public Material pointCloudMaterial;
    public float pointSize = 0.005f;

    private Queue<(Vector3[] points, Color32[] colors)> pointCloudQueue = new();
    private const int maxQueueSize = 5;

    private Mesh mesh;
    private readonly List<Vector3> vertices = new();
    private readonly List<Color32> colors = new();
    private readonly List<Vector2> offsetIndices = new();
    private readonly List<int> indices = new();

    private static readonly float[] baseOffsetIndices = new float[] { 0, 1, 2, 3, 4, 5 };

    void Start()
    {
        webRTCManager = FindObjectOfType<WebRTCManager>();
        if (webRTCManager == null)
        {
            Debug.LogError("WebRTCManager not found!");
            enabled = false;
            return;
        }

        mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.MarkDynamic(); // Hint for performance
        GetComponent<MeshFilter>().sharedMesh = mesh;

        GetComponent<MeshRenderer>().material = pointCloudMaterial;
        pointCloudMaterial.SetFloat("_PointSize", pointSize * transform.localScale.x);
    }

    void Update()
    {
        if (webRTCManager.HasNewPointCloud())
        {
            var (pts, cols) = webRTCManager.GetReceivedPointCloud();
            if (pointCloudQueue.Count >= maxQueueSize)
                pointCloudQueue.Dequeue();

            pointCloudQueue.Enqueue((pts, cols));
        }

        if (pointCloudQueue.Count > 0)
        {
            var (positions, colorData) = pointCloudQueue.Dequeue();
            UpdateMesh(positions, colorData);
        }
    }

    void UpdateMesh(Vector3[] positions, Color32[] colorData)
    {
        int count = Mathf.Min(positions.Length, colorData.Length);
        if (count == 0) return;

        mesh.Clear();
        vertices.Clear();
        offsetIndices.Clear();
        colors.Clear();
        indices.Clear();

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = positions[i];
            Color32 col = colorData[i];

            for (int j = 0; j < 6; j++)
            {
                vertices.Add(pos);
                offsetIndices.Add(new Vector2(baseOffsetIndices[j], 0));
                colors.Add(col);
                indices.Add(i * 6 + j);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, offsetIndices);
        mesh.SetColors(colors);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
    }
}