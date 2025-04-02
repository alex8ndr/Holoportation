using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class ElemRenderer : MonoBehaviour
{
    private Mesh mesh;
    private Vector3[] vertices;
    private Color[] colors;
    private int[] indices;

    private WebRTCManager webRTCManager;
    public Material pointCloudMaterial;

    private bool hasNewData = false;

    private float timeSinceLastRender = 0.0f;
    private float totalTime = 0.0f;
    private float averageFPS = 0.0f;
    private int numFrames = 0;
    private bool startedCounter = false;

    void Start()
    {
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Supports large vertex counts
        mesh.MarkDynamic(); // Marks mesh for frequent updates

        GetComponent<MeshFilter>().mesh = mesh;
        webRTCManager = GameObject.FindObjectOfType<WebRTCManager>();

        if (webRTCManager == null)
        {
            Debug.LogError("WebRTCManager not found!");
            return;
        }
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
        }

        if (webRTCManager.HasNewPointCloud())
        {
            startedCounter = true;
            (vertices, colors) = webRTCManager.GetReceivedPointCloud();
            hasNewData = true;
        }

        if (hasNewData)
        {
            UpdateMesh();
            hasNewData = false;
        }
    }

    private void UpdateMesh()
    {
        int pointCount = vertices.Length;
        if (indices == null || indices.Length != pointCount)
        {
            indices = new int[pointCount];
            for (int i = 0; i < pointCount; i++) indices[i] = i;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.SetIndices(indices, MeshTopology.Points, 0);
        mesh.UploadMeshData(false); // Keeps GPU memory without reallocation

        totalTime += timeSinceLastRender;
        timeSinceLastRender = 0.0f;
        numFrames++;
        averageFPS = numFrames / totalTime;
        Debug.Log("Average FPS: " + averageFPS);
    }
}