using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

public class PointCloudReceiver : MonoBehaviour
{
    TcpClient socket;
    public string IPAddress = "127.0.0.1";
    public int port = 48002;
    public WebRTCManager webRTCManager;

    private bool isConnected = false;

    void Start()
    {
        Connect(IPAddress);
    }

    public async void Connect(string IP)
    {
        socket = new TcpClient();
        try
        {
            await socket.ConnectAsync(IP, port);
            isConnected = true;
            StartReceivingLoop();
        }
        catch (Exception e)
        {
            Debug.LogError("Connection to LiveScan3D failed: " + e.Message);
        }
    }

    private async void StartReceivingLoop()
    {
        while (isConnected && socket.Connected)
        {
            try
            {
                // Request a new frame
                await socket.GetStream().WriteAsync(new byte[] { 0 });

                // --- Read scale factor (short) ---
                byte[] scale = new byte[sizeof(short)];
                int bytesRead = 0;
                while (bytesRead < sizeof(short))
                    bytesRead += await socket.GetStream().ReadAsync(scale, bytesRead, sizeof(short) - bytesRead);

                // --- Read number of points (4 bytes) ---
                int nPointsToRead = await ReadIntAsync();
                int nVerticesBytes = sizeof(short) * 3 * nPointsToRead;
                int nColorsBytes = 3 * nPointsToRead;

                byte[] vertices = new byte[nVerticesBytes];
                byte[] colors = new byte[nColorsBytes];

                // Read vertex data
                bytesRead = 0;
                while (bytesRead < nVerticesBytes)
                    bytesRead += await socket.GetStream().ReadAsync(vertices, bytesRead, Math.Min(nVerticesBytes - bytesRead, 64000));

                // Read color data
                bytesRead = 0;
                while (bytesRead < nColorsBytes)
                    bytesRead += await socket.GetStream().ReadAsync(colors, bytesRead, Math.Min(nColorsBytes - bytesRead, 64000));

                float floatscale = BitConverter.ToInt16(scale, 0);

                Debug.Log($"Received {nPointsToRead} points with scale " + floatscale);

                // Send to WebRTCManager along with the scale
                webRTCManager.SendPointCloud(scale, vertices, colors);
            }
            catch (Exception e)
            {
                Debug.LogError("Error receiving frame: " + e.Message);
            }
        }
    }

    private async Task<int> ReadIntAsync()
    {
        byte[] buffer = new byte[4];
        int bytesRead = 0;

        while (bytesRead < 4)
        {
            bytesRead += await socket.GetStream().ReadAsync(buffer, bytesRead, 4 - bytesRead);
        }

        return BitConverter.ToInt32(buffer, 0);
    }

    private void OnDestroy()
    {
        isConnected = false;
        socket.Dispose();
    }
}
