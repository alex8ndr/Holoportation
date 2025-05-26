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
            StartReceivingLoop();
        }
        catch (Exception e)
        {
            Debug.LogError("Connection to LiveScan3D failed: " + e.Message);
        }
    }

    private async void StartReceivingLoop()
    {
        while (socket.Connected)
        {
            try
            {
                // Request a new frame
                await socket.GetStream().WriteAsync(new byte[] { 0 });

                // Read frame data
                int nPointsToRead = await ReadIntAsync();
                int nVerticesBytes = sizeof(short) * 3 * nPointsToRead;
                int nColorsBytes = 3 * nPointsToRead;

                byte[] vertices = new byte[nVerticesBytes];
                byte[] colors = new byte[nColorsBytes];

                int bytesRead = 0;
                while (bytesRead < nVerticesBytes)
                    bytesRead += await socket.GetStream().ReadAsync(vertices, bytesRead, Math.Min(nVerticesBytes - bytesRead, 64000));

                bytesRead = 0;
                while (bytesRead < nColorsBytes)
                    bytesRead += await socket.GetStream().ReadAsync(colors, bytesRead, Math.Min(nColorsBytes - bytesRead, 64000));

                // Directly pass to WebRTCManager
                webRTCManager.SendPointCloud(vertices, colors);
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
}
