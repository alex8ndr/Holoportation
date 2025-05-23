using System.Collections.Generic;
using UnityEngine;

public class ElemRenderer : MonoBehaviour
{
    private WebRTCManager webRTCManager;
    public Material pointCloudMaterial;
    public float pointSize = 0.005f;

    private Queue<(Vector3[] points, Color[] colors)> pointCloudQueue = new();
    private const int maxQueueSize = 5;

    private Matrix4x4[] matrices;
    private Vector4[] colorData;
    private int previousColorCapacity = 0;

    private MaterialPropertyBlock propertyBlock;
    private RenderParams renderParams;

    private Mesh quadMesh;
    private bool needsRender = false;

    private int pointCount = 0;
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

        CreateQuadMesh();

        propertyBlock = new MaterialPropertyBlock();

        renderParams = new RenderParams(pointCloudMaterial)
        {
            matProps = propertyBlock,
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000f)
        };
    }

    void Update()
    {
        if (counterStarted)
            timeSinceLastRender += Time.deltaTime;

        if (webRTCManager.HasNewPointCloud())
        {
            counterStarted = true;
            needsRender = true;
            var (points, colors) = webRTCManager.GetReceivedPointCloud();

            if (pointCloudQueue.Count >= maxQueueSize)
                pointCloudQueue.Dequeue();

            pointCloudQueue.Enqueue((points, colors));
        }

        if (pointCloudQueue.Count > 0)
        {
            var (positions, colors) = pointCloudQueue.Dequeue();
            UpdateInstanceData(positions, colors);
        }

        if (needsRender)
        {
            totalTime += timeSinceLastRender;
            timeSinceLastRender = 0.0f;
            numFrames++;
            averageFPS = numFrames / totalTime;
            Debug.Log("Average FPS: " + averageFPS);
            needsRender = false;
        }

        RenderPointCloud();
    }

    private void UpdateInstanceData(Vector3[] positions, Color[] colors)
    {
        pointCount = positions.Length;
        if (pointCount == 0) return;

        pointCount = Mathf.Min(positions.Length, colors.Length);

        matrices = new Matrix4x4[pointCount];
        colorData = new Vector4[pointCount];

        Vector3 right = Camera.main.transform.right;
        Vector3 up = Camera.main.transform.up;

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 pos = positions[i];
            matrices[i] = Matrix4x4.Translate(pos); // position only — rotation in shader
            colorData[i] = colors[i];
        }

        // 💡 Only recreate the block if more data is needed
        if (propertyBlock == null || pointCount > previousColorCapacity)
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
            else
                propertyBlock.Clear();

            previousColorCapacity = pointCount;
            renderParams.matProps = propertyBlock;
        }

        propertyBlock.SetVectorArray("_Colors", colorData);
    }

    private void RenderPointCloud()
    {
        if (pointCount == 0 || quadMesh == null || pointCloudMaterial == null)
            return;

        Graphics.RenderMeshInstanced(renderParams, quadMesh, 0, matrices, pointCount);
    }

    private void CreateQuadMesh()
    {
        quadMesh = new Mesh();
        quadMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;

        float s = pointSize; // scale factor

        quadMesh.vertices = new[]
        {
            new Vector3(-0.5f * s, -0.5f * s, 0),
            new Vector3( 0.5f * s, -0.5f * s, 0),
            new Vector3(-0.5f * s,  0.5f * s, 0),
            new Vector3( 0.5f * s,  0.5f * s, 0)
        };

        quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };

        quadMesh.uv = new[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };

        quadMesh.RecalculateNormals();
        quadMesh.UploadMeshData(true);
    }
}
