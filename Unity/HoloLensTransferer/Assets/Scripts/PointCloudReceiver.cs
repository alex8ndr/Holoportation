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
                int nBytesToRead = 3 * nPointsToRead;

                byte[] vertices = new byte[nBytesToRead];
                byte[] colors = new byte[nBytesToRead];

                int bytesRead = 0;
                while (bytesRead < nBytesToRead)
                    bytesRead += await socket.GetStream().ReadAsync(vertices, bytesRead, Math.Min(nBytesToRead - bytesRead, 64000));

                bytesRead = 0;
                while (bytesRead < nBytesToRead)
                    bytesRead += await socket.GetStream().ReadAsync(colors, bytesRead, Math.Min(nBytesToRead - bytesRead, 64000));

                Debug.Log("Received " + nPointsToRead + " points");

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

    private void OnDestroy()
    {
        socket.Close();
    }
}
