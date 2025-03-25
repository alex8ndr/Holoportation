using Fusion;
using System;
using UnityEngine;

public class ElemRendererOptimized : NetworkBehaviour
{
    // Define buffers for points, expanded billboards, and colors
    private ComputeBuffer positionBuffer;
    private ComputeBuffer colorBuffer;
    private ComputeBuffer billboardVertexBuffer;
    private ComputeBuffer billboardColorBuffer;

    private WebRTCManager webRTCManager;
    private ComputeShader computeShader;
    private Material material;

    // Point cloud parameters
    public int numPoints = 20000; // Adjust accordingly
    public float pointSize = 0.01f;

    private int kernelHandle;
    private Matrix4x4 viewProjection;

    private float totalTime = 0.0f;
    private float averageFPS = 0.0f;
    private int numFrames = 0;

    void Start()
    {
        webRTCManager = GameObject.FindObjectOfType<WebRTCManager>();

        if (webRTCManager == null)
        {
            Debug.LogError("WebRTCManager is not assigned!");
            return;
        }

        // Setup Compute Shader
        computeShader = Resources.Load<ComputeShader>("PointCloudComputeShader");
        material = new Material(Shader.Find("Custom/GS Billboard")); // Or another custom shader

        kernelHandle = computeShader.FindKernel("CSMain");

        InitBuffers();
    }

    void InitBuffers()
    {
        // Initialize buffers for point cloud
        positionBuffer = new ComputeBuffer(numPoints, sizeof(float) * 3);
        colorBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);

        // Buffers for billboard vertices and colors (4 vertices per point for the billboard quad)
        billboardVertexBuffer = new ComputeBuffer(numPoints * 4, sizeof(float) * 4);  // 4 vertices per point
        billboardColorBuffer = new ComputeBuffer(numPoints * 4, sizeof(float) * 4);    // Color per vertex

        computeShader.SetBuffer(kernelHandle, "PositionBuffer", positionBuffer);
        computeShader.SetBuffer(kernelHandle, "ColorBuffer", colorBuffer);
        computeShader.SetBuffer(kernelHandle, "BillboardVertices", billboardVertexBuffer);
        computeShader.SetBuffer(kernelHandle, "BillboardColors", billboardColorBuffer);
    }

    void Update()
    {
        totalTime += Time.deltaTime;

        // Fetch the new point cloud from WebRTCManager
        if (webRTCManager.HasNewPointCloud())
        {
            Vector3[] vertices;
            Color[] colors;
            (vertices, colors) = webRTCManager.GetReceivedPointCloud();

            // Update buffers
            positionBuffer.SetData(vertices);
            colorBuffer.SetData(colors);

            // Set parameters for the Compute Shader
            viewProjection = Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix;
            computeShader.SetMatrix("ViewProjection", viewProjection);
            computeShader.SetVector("CameraPos", Camera.main.transform.position);
            computeShader.SetFloat("PointSize", pointSize);
            computeShader.SetInt("NumPoints", vertices.Length);

            // Dispatch the compute shader
            computeShader.Dispatch(kernelHandle, Mathf.CeilToInt(vertices.Length / 256f), 1, 1);

            // Send the buffers to the shader for rendering
            material.SetBuffer("BillboardVertices", billboardVertexBuffer);
            material.SetBuffer("BillboardColors", billboardColorBuffer);

            // Draw billboards with indirect rendering
            Graphics.DrawProcedural(material, new Bounds(Vector3.zero, Vector3.one * 10), MeshTopology.Quads, vertices.Length * 4);

            numFrames++;
            averageFPS = numFrames / totalTime;
            Debug.Log("Average FPS: " + averageFPS);
        }
    }

    void OnDestroy()
    {
        // Clean up buffers
        positionBuffer.Release();
        colorBuffer.Release();
        billboardVertexBuffer.Release();
        billboardColorBuffer.Release();
    }
}