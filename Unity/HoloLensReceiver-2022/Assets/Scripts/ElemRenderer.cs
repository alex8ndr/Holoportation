using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ElemRenderer : MonoBehaviour
{
    private WebRTCManager webRTCManager;
    public Material pointCloudMaterial;
    public float pointSize = 0.005f;

    private bool isUpdating = false;

    private Queue<(Vector3[] points, Color[] colors)> pointCloudQueue = new Queue<(Vector3[], Color[])>();
    private const int maxQueueSize = 5;

    private Mesh mesh;

    private bool counterStarted = false;
    private float timeSinceLastRender = 0.0f;
    private float totalTime = 0.0f;
    private int numFrames = 0;
    private float averageFPS = 0.0f;

    void Start()
    {
        webRTCManager = FindObjectOfType<WebRTCManager>();

        if (webRTCManager == null)
        {
            Debug.LogError("WebRTCManager not found!");
            return;
        }

        GetComponent<MeshRenderer>().material = pointCloudMaterial;

        pointCloudMaterial.SetFloat("_PointSize", pointSize * transform.localScale.x);
    }

    void Update()
    {
        if (counterStarted)
        {
            timeSinceLastRender += Time.deltaTime;
        }

        // Enqueue new frame
        if (webRTCManager.HasNewPointCloud())
        {
            if (!counterStarted)
            {
                counterStarted = true;
            }

            var (points, colors) = webRTCManager.GetReceivedPointCloud();

            if (pointCloudQueue.Count >= maxQueueSize)
                pointCloudQueue.Dequeue(); // Discard oldest

            pointCloudQueue.Enqueue((points, colors));
        }

        // Dequeue and process one frame per update
        if (!isUpdating && pointCloudQueue.Count > 0)
        {
            var (points, colors) = pointCloudQueue.Dequeue();
            SetPointCloud(points, colors);
        }
    }

    public void SetPointCloud(Vector3[] positions, Color[] colors)
    {
        if (positions == null || positions.Length == 0)
            return;

        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        List<Vector3> verts = new List<Vector3>();
        List<Color> cols = new List<Color>();
        List<Vector2> offsets = new List<Vector2>();
        List<int> indices = new List<int>();

        Vector2[] baseOffsets = new Vector2[]
        {
            new Vector2(-0.5f, -0.5f),
            new Vector2( 0.5f, -0.5f),
            new Vector2(-0.5f,  0.5f),
            new Vector2( 0.5f, -0.5f),
            new Vector2( 0.5f,  0.5f),
            new Vector2(-0.5f,  0.5f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            Color color = (i < colors.Length) ? colors[i] : Color.white;

            for (int j = 0; j < 6; j++)
            {
                verts.Add(positions[i]);
                offsets.Add(baseOffsets[j]);
                cols.Add(color);
                indices.Add(i * 6 + j);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetUVs(0, offsets); // Send offset in TEXCOORD0
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        GetComponent<MeshFilter>().sharedMesh = mesh;

        totalTime += timeSinceLastRender;
        timeSinceLastRender = 0.0f;
        numFrames++;

        averageFPS = numFrames / totalTime;
        Debug.Log("Average FPS: " + averageFPS);
    }
}