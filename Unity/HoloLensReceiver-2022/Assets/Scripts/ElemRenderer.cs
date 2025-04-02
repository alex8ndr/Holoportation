using System.Collections.Generic;
using UnityEngine;

public class ElemRenderer : MonoBehaviour
{
    private ComputeBuffer pointCloudBuffer;
    private ComputeBuffer colorBuffer;

    private ComputeShader pointCloudComputeShader; // Reference to the compute shader

    private int numPoints = 0;

    private WebRTCManager webRTCManager;
    public Material pointCloudMaterial;
    public float pointSize = 0.005f;

    private float timeSinceLastRender = 0.0f;
    private float totalTime = 0.0f;
    private float averageFPS = 0.0f;
    private int numFrames = 0;
    private bool startedCounter = false;

    void Start()
    {
        webRTCManager = FindObjectOfType<WebRTCManager>();

        if (webRTCManager == null)
        {
            Debug.LogError("WebRTCManager not found!");
            return;
        }

        pointCloudComputeShader = Resources.Load<ComputeShader>("PointCloudCompute");

        InitializeComputeBuffer(30000); // Initial buffer size

        pointCloudMaterial.SetFloat("_PointSize", pointSize * transform.localScale.x);
    }

    void Update()
    {
        if (startedCounter)
        {
            timeSinceLastRender += Time.deltaTime;
        }

        if (pointCloudMaterial != null)
        {
            // Pass camera position and rotation
            pointCloudMaterial.SetVector("_CameraPosition", Camera.main.transform.position);
            pointCloudMaterial.SetMatrix("_CameraRotation", Camera.main.transform.localToWorldMatrix);
        }

        if (webRTCManager.HasNewPointCloud())
        {
            startedCounter = true;
            var (points, colors) = webRTCManager.GetReceivedPointCloud();
            var colorData = ConvertToVector3Array(colors);
            UpdateComputeBuffer(points, colorData);
        }

        // Dispatch the compute shader to process the point cloud data
        DispatchComputeShader();
    }

    private void InitializeComputeBuffer(int maxPoints)
    {
        // Store 12 bytes per point (Vector3) for positions (x, y, z)
        pointCloudBuffer = new ComputeBuffer(maxPoints, sizeof(float) * 3, ComputeBufferType.Default);

        // Store 12 bytes per point (Vector3) for colors (r, g, b)
        colorBuffer = new ComputeBuffer(maxPoints, sizeof(float) * 3, ComputeBufferType.Default);

        pointCloudMaterial.SetBuffer("_PointCloudBuffer", pointCloudBuffer);
        pointCloudMaterial.SetBuffer("_ColorBuffer", colorBuffer);
    }

    private void UpdateComputeBuffer(Vector3[] newPointData, Vector3[] newColorData)
    {
        if (newPointData.Length > pointCloudBuffer.count)
        {
            pointCloudBuffer.Release();
            colorBuffer.Release();
            InitializeComputeBuffer(newPointData.Length);
        }

        // Set the data for both point positions and colors
        pointCloudBuffer.SetData(newPointData);
        colorBuffer.SetData(newColorData);

        numPoints = newPointData.Length;
        pointCloudMaterial.SetInt("_NumPoints", numPoints);

        totalTime += timeSinceLastRender;
        timeSinceLastRender = 0.0f;
        numFrames++;
        averageFPS = numFrames / totalTime;
        Debug.Log("Average FPS: " + averageFPS);
    }

    private Vector3[] ConvertToVector3Array(Color[] colors)
    {
        int length = colors.Length;
        Vector3[] pointData = new Vector3[length];

        for (int i = 0; i < length; i++)
        {
            // Store the color in a Vector3 with r, g, b
            pointData[i] = new Vector3(colors[i].r, colors[i].g, colors[i].b);
        }

        return pointData;
    }

    private void DispatchComputeShader()
    {
        int kernelHandle = pointCloudComputeShader.FindKernel("CSMain");

        pointCloudComputeShader.SetBuffer(kernelHandle, "_PointCloudBuffer", pointCloudBuffer);
        pointCloudComputeShader.SetBuffer(kernelHandle, "_ColorBuffer", colorBuffer);
        pointCloudComputeShader.SetVector("_CameraPosition", Camera.main.transform.position);

        // Dispatch the compute shader (run it in parallel across all points)
        int threadGroups = Mathf.CeilToInt(numPoints / 64.0f);
        pointCloudComputeShader.Dispatch(kernelHandle, threadGroups, 1, 1);
    }

    void OnRenderObject()
    {
        if (pointCloudMaterial == null || pointCloudBuffer == null || numPoints == 0)
            return;

        pointCloudMaterial.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Points, numPoints);
    }

    private void OnDestroy()
    {
        if (pointCloudBuffer != null)
        {
            pointCloudBuffer.Release();
        }

        if (colorBuffer != null)
        {
            colorBuffer.Release();
        }
    }
}