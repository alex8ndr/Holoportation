using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ElemRenderer : MonoBehaviour
{
    private ComputeBuffer pointCloudBuffer;
    private ComputeBuffer colorBuffer;

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

        GetComponent<MeshRenderer>().material = pointCloudMaterial;

        InitializeComputeBuffer(100000); // Initial buffer size

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

            pointCloudMaterial.SetBuffer("_PointCloudBuffer", pointCloudBuffer);
            pointCloudMaterial.SetBuffer("_ColorBuffer", colorBuffer);
        }

        if (webRTCManager.HasNewPointCloud())
        {
            startedCounter = true;
            var (points, colors) = webRTCManager.GetReceivedPointCloud();
            var colorData = ConvertToVector3Array(colors);
            StartCoroutine(UpdateBuffersWithDelay(points, colorData));
        }
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

    IEnumerator UpdateBuffersWithDelay(Vector3[] points, Vector3[] colors)
    {
        yield return null; // Just delay by one frame (prevents race conditions)
        UpdateComputeBuffer(points, colors);
    }

    private void UpdateComputeBuffer(Vector3[] newPointData, Vector3[] newColorData)
    {
        // If too many points, discard extra but don't reallocate
        int clampedCount = Mathf.Min(newPointData.Length, pointCloudBuffer.count);

        Vector3[] paddedPoints = new Vector3[pointCloudBuffer.count];
        Vector3[] paddedColors = new Vector3[colorBuffer.count];

        System.Array.Copy(newPointData, paddedPoints, clampedCount);
        System.Array.Copy(newColorData, paddedColors, clampedCount);

        pointCloudBuffer.SetData(paddedPoints);
        colorBuffer.SetData(paddedColors);

        numPoints = clampedCount;
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
    }
}