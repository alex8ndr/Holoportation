using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ElemRenderer : MonoBehaviour
{
    private WebRTCManager webRTCManager;
    public Material pointCloudMaterial;
    public float pointSize = 0.005f;

    private Queue<(Vector3[] points, Color[] colors)> pointCloudQueue = new();
    private const int maxQueueSize = 5;

    private Mesh mesh;
    private readonly List<Vector3> vertices = new();
    private readonly List<Color> colors = new();
    private readonly List<Vector2> offsets = new();
    private readonly List<int> indices = new();

    private static readonly Vector2[] baseOffsets = new Vector2[]
    {
        new(-0.5f, -0.5f), new(0.5f, -0.5f),
        new(-0.5f,  0.5f), new(0.5f, -0.5f),
        new(0.5f,  0.5f), new(-0.5f, 0.5f)
    };

    private float timeSinceLastRender = 0f, totalTime = 0f;
    private int numFrames = 0;
    private bool started = false;

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
        GetComponent<MeshFilter>().sharedMesh = mesh;

        GetComponent<MeshRenderer>().material = pointCloudMaterial;
        pointCloudMaterial.SetFloat("_PointSize", pointSize * transform.localScale.x);
    }

    void Update()
    {
        if (started)
            timeSinceLastRender += Time.deltaTime;

        if (webRTCManager.HasNewPointCloud())
        {
            if (!started)
                started = true;

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

    void UpdateMesh(Vector3[] positions, Color[] colorData)
    {
        int count = Mathf.Min(positions.Length, colorData.Length);
        if (count == 0) return;

        mesh.Clear();
        vertices.Clear();
        offsets.Clear();
        colors.Clear();
        indices.Clear();

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = positions[i];
            Color col = colorData[i];

            for (int j = 0; j < 6; j++)
            {
                vertices.Add(pos);
                offsets.Add(baseOffsets[j]);
                colors.Add(col);
                indices.Add(i * 6 + j);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, offsets);
        mesh.SetColors(colors);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        GetComponent<MeshFilter>().sharedMesh = mesh;

        totalTime += timeSinceLastRender;
        timeSinceLastRender = 0f;
        numFrames++;
        Debug.Log($"Avg FPS: {(numFrames / totalTime):F2}");
    }
}