using Fusion;
using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class DocumentPictureReceiver : MonoBehaviour
{
    public string serverIP = "10.69.55.216";
    public int port = 48004;
    public Renderer targetRenderer;
    public float maxImageSize = 3.0f;
    public float minImageSize = 1.0f;

    private TcpListener listener;
    private Thread listenerThread;
    private bool isRunning = false;
    private float lastImageTime = 0f;
    private const float IMAGE_TIMEOUT = 30f;

    private byte[] receivedImageData;
    private int newWidth = 0;
    private int newHeight = 0;

    private float xScaleUnitWidth;
    private float zScaleUnitHeight;
    private const float pixelToMeter = 0.26f / 1000f; // Convert pixels to meters
    private bool isProcessingImage = false;

    private readonly object lockObject = new object(); // Ensure thread safety

    void Start()
    {
        if (targetRenderer == null)
        {
            Debug.LogError("Target Renderer is not assigned!");
            return;
        }

        targetRenderer.enabled = false; // Hide initially

        // Get the current world-space width and height of the plane
        float currentWorldWidth = targetRenderer.localBounds.size.x;
        float currentWorldHeight = targetRenderer.localBounds.size.z;

        // Get the current local scale
        Vector3 localScale = targetRenderer.transform.localScale;

        // Compute the unit local x-scale and z-scale
        xScaleUnitWidth = (1.0f * localScale.x) / currentWorldWidth;
        zScaleUnitHeight = (1.0f * localScale.z) / currentWorldHeight;

        isRunning = true;
        listenerThread = new Thread(ListenForImages);
        listenerThread.IsBackground = true;
        listenerThread.Start();
    }

    void ListenForImages()
    {
        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Debug.Log("Listening for images on " + serverIP + ":" + port);

            while (isRunning)
            {
                using (TcpClient client = listener.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    // Read image dimensions first
                    int height = reader.ReadInt32();
                    int width = reader.ReadInt32();

                    // Read image size
                    int imageSize = reader.ReadInt32();
                    byte[] imageData = reader.ReadBytes(imageSize);

                    if (imageData.Length > 0)
                    {
                        // Store data safely to be processed in the main thread
                        lock (lockObject)
                        {
                            receivedImageData = imageData;
                            newWidth = width;
                            newHeight = height;
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("TCP Listener Error: " + e.Message);
        }
    }

    void Update()
    {
        // Process image only on the main thread
        if (receivedImageData != null && !isProcessingImage)
        {
            isProcessingImage = true;
        }

        // Check that the image was received here and fully sent through WebRTC
        if (isProcessingImage)
        {
            ApplyTexture();
            isProcessingImage = false;
        }

        // Hide renderer if no new image has been received in the timeout period
        if (Time.time - lastImageTime > IMAGE_TIMEOUT && targetRenderer.enabled)
        {
            targetRenderer.enabled = false;
            Debug.Log("No new image in over " + IMAGE_TIMEOUT + " seconds, hiding display");
        }
    }

    private void ApplyTexture()
    {
        byte[] imageData = receivedImageData; // Get image data from WebRTCManager

        if (imageData == null || imageData.Length == 0)
        {
            Debug.LogError("Failed to retrieve image data from WebRTCManager.");
            return;
        }

        Texture2D texture = new Texture2D(2, 2);
        if (texture.LoadImage(imageData))
        {
            texture.Apply();
            targetRenderer.material.mainTexture = texture;
            targetRenderer.enabled = true;
            lastImageTime = Time.time;

            // Scale the renderer plane to match the aspect ratio
            AdjustRendererScale();
        }
        else
        {
            Debug.LogError("Failed to load image data into texture");
        }
    }

    private void AdjustRendererScale()
    {
        float aspectRatio = (float)newWidth / (float)newHeight;
        float realWidth = (float)newWidth * pixelToMeter;
        float realHeight = (float)newHeight * pixelToMeter;

        realWidth = Mathf.Clamp(realWidth, minImageSize, maxImageSize);
        realHeight = realWidth / aspectRatio;

        realHeight = Mathf.Clamp(realHeight, minImageSize, maxImageSize);
        realWidth = realHeight * aspectRatio;

        Vector3 newScale = targetRenderer.transform.localScale;
        newScale.x = realWidth * xScaleUnitWidth;  // Width
        newScale.z = realHeight * zScaleUnitHeight; // Height

        targetRenderer.transform.localScale = newScale;
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        listener?.Stop();
        listenerThread?.Abort();
    }
}