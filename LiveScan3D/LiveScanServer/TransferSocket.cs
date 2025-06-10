using System;
using System.Collections.Generic;

using System.Net.Sockets;

namespace KinectServer
{
    public class TransferSocket
    {
        TcpClient oSocket;

        // Parameters used to find the scale (precision level of the point cloud)
        private const short MAX_SCALE = 2500;
        private const short MIN_SCALE = 400;
        private const double SCALE_FN_OFFSET = 7400;
        private const double SCALE_FN_FACTOR = 580;

        public TransferSocket(TcpClient clientSocket)
        {
            oSocket = clientSocket;
        }

        public byte[] Receive(int nBytes)
        {
            byte[] buffer;
            if (oSocket.Available != 0)
            {
                buffer = new byte[Math.Min(nBytes, oSocket.Available)];
                oSocket.GetStream().Read(buffer, 0, nBytes);
            }
            else
                buffer = new byte[0];

            return buffer;
        }

        public bool SocketConnected()
        {
            return oSocket.Connected;
        }

        public void WriteInt(int val)
        {
            oSocket.GetStream().Write(BitConverter.GetBytes(val), 0, 4);
        }

        public void WriteFloat(float val)
        {
            oSocket.GetStream().Write(BitConverter.GetBytes(val), 0, 4);
        }

        public void SendFrame(List<float> vertices, List<byte> colors)
        {
            int originalVertexCount = vertices.Count / 3;
            short scale = DetermineScale(originalVertexCount);

            HashSet<(short, short, short)> uniquePoints = new HashSet<(short, short, short)>();
            List<short> filteredVertices = new List<short>();
            List<byte> filteredColors = new List<byte>();

            for (int i = 0; i < vertices.Count; i += 3)
            {
                short x = (short)(vertices[i] * scale);
                short y = (short)(vertices[i + 1] * scale);
                short z = (short)(vertices[i + 2] * scale);

                var point = (x, y, z);
                if (uniquePoints.Add(point))
                {
                    filteredVertices.Add(x);
                    filteredVertices.Add(y);
                    filteredVertices.Add(z);

                    // Copy corresponding RGB color
                    int colorIndex = i;
                    filteredColors.Add(colors[colorIndex]);
                    filteredColors.Add(colors[colorIndex + 1]);
                    filteredColors.Add(colors[colorIndex + 2]);
                }
            }

            int nVerticesToSend = filteredVertices.Count / 3;
            byte[] buffer = new byte[sizeof(short) * filteredVertices.Count];
            Buffer.BlockCopy(filteredVertices.ToArray(), 0, buffer, 0, buffer.Length);

            try
            {
                // Send the scale first
                byte[] scaleBytes = BitConverter.GetBytes(scale);
                oSocket.GetStream().Write(scaleBytes, 0, scaleBytes.Length);

                WriteInt(nVerticesToSend);
                oSocket.GetStream().Write(buffer, 0, buffer.Length);
                oSocket.GetStream().Write(filteredColors.ToArray(), 0, filteredColors.Count);
            }
            catch (Exception ex)
            {
            }
        }

        // Determine scale based on number of vertices
        private short DetermineScale(int vertexCount)
        {
            if (vertexCount <= 0) return MAX_SCALE;
            short scale = (short)Math.Truncate(SCALE_FN_OFFSET - SCALE_FN_FACTOR * Math.Log(vertexCount));
            return Math.Min(MAX_SCALE, Math.Max(scale, MIN_SCALE)); // Clamp between min and max acceptable scales
        }
    }
}