using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ElemRenderer : MonoBehaviour
{
    private ComputeBuffer pointCloudBuffer;
    private ComputeBuffer colorBuffer;

    private int numPoints = 0;

    public Material pointCloudMaterial;
    public float pointSize = 0.005f;

    private float timeSinceLastRender = 0.0f;
    private float totalTime = 0.0f;
    private float averageFPS = 0.0f;
    private int numFrames = 0;
    private bool startedCounter = false;

    private bool startUpdate = false;
    private Vector3[] newPoints;
    private Color[] newColors;
    private bool isUpdating = false;

    private Queue<(Vector3[] points, Vector3[] colors)> pointCloudQueue = new Queue<(Vector3[], Vector3[])>();
    private const int maxQueueSize = 5;

    void Start()
    {
        GetComponent<MeshRenderer>().material = pointCloudMaterial;

        InitializeComputeBuffer(25000); // Initial buffer size

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
            pointCloudMaterial.SetVector("_CameraPosition", Camera.main.transform.position);
            pointCloudMaterial.SetMatrix("_CameraRotation", Camera.main.transform.localToWorldMatrix);

            pointCloudMaterial.SetBuffer("_PointCloudBuffer", pointCloudBuffer);
            pointCloudMaterial.SetBuffer("_ColorBuffer", colorBuffer);
        }

        // Enqueue new frame
        if (startUpdate)
        {
            startedCounter = true;
            var colorData = ConvertToVector3Array(newColors);

            if (pointCloudQueue.Count >= maxQueueSize)
                pointCloudQueue.Dequeue(); // Discard oldest

            pointCloudQueue.Enqueue((newPoints, colorData));
        }

        // Dequeue and process one frame per update
        if (!isUpdating && pointCloudQueue.Count > 0)
        {
            var (points, colors) = pointCloudQueue.Dequeue();
            StartCoroutine(UpdateBuffersWithDelay(points, colors));
        }
    }

    public void TriggerUpdate(Vector3[] newPoints, Color[] newColors)
    {
        startUpdate = true;
        this.newPoints = newPoints;
        this.newColors = newColors;
    }

    private void InitializeComputeBuffer(int maxPoints)
    {
        pointCloudBuffer = new ComputeBuffer(maxPoints, sizeof(float) * 3, ComputeBufferType.Default);
        colorBuffer = new ComputeBuffer(maxPoints, sizeof(float) * 3, ComputeBufferType.Default);

        pointCloudMaterial.SetBuffer("_PointCloudBuffer", pointCloudBuffer);
        pointCloudMaterial.SetBuffer("_ColorBuffer", colorBuffer);
    }

    IEnumerator UpdateBuffersWithDelay(Vector3[] points, Vector3[] colors)
    {
        isUpdating = true;
        yield return new WaitForEndOfFrame(); // Wait for rendering to finish
        UpdateComputeBuffer(points, colors);
        yield return null;
        isUpdating = false;
        startUpdate = false;

        totalTime += timeSinceLastRender;
        timeSinceLastRender = 0.0f;
        numFrames++;
        averageFPS = numFrames / totalTime;
        Debug.Log("Average FPS: " + averageFPS);
    }

    private void UpdateComputeBuffer(Vector3[] newPointData, Vector3[] newColorData)
    {
        int requiredCount = newPointData.Length;

        if (requiredCount > pointCloudBuffer.count)
        {
            // Release and recreate buffers with new size
            Debug.Log($"Resizing compute buffer to {requiredCount} points.");
            pointCloudBuffer.Release();
            colorBuffer.Release();
            InitializeComputeBuffer(requiredCount);
        }

        Vector3[] paddedPoints = new Vector3[pointCloudBuffer.count];
        Vector3[] paddedColors = new Vector3[colorBuffer.count];

        System.Array.Copy(newPointData, paddedPoints, newPointData.Length);
        System.Array.Copy(newColorData, paddedColors, newColorData.Length);

        pointCloudBuffer.SetData(paddedPoints);
        colorBuffer.SetData(paddedColors);

        numPoints = newPointData.Length;
        pointCloudMaterial.SetInt("_NumPoints", numPoints);
    }

    private Vector3[] ConvertToVector3Array(Color[] colors)
    {
        int length = colors.Length;
        Vector3[] pointData = new Vector3[length];

        for (int i = 0; i < length; i++)
        {
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
        pointCloudBuffer?.Release();
        colorBuffer?.Release();
    }
}