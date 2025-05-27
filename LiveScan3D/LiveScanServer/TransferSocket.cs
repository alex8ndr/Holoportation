using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;

namespace KinectServer
{
    public class TransferSocket
    {
        TcpClient oSocket;

        // Define quantization range per axis (in meters)
        float xMin = -0.32f, xMax = 0.32f;
        float yMin = -0.32f, yMax = 0.32f;
        float zMin = 0.00f, zMax = 0.64f; // Z starts at 0

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

        public void SendFrame(List<float> vertices, List<byte> colors, float precision = 0.0025f)
        {
            int nPoints = vertices.Count / 3;

            List<byte> uniqueVertices = new List<byte>(nPoints * 3);
            List<byte> uniqueColors = new List<byte>(nPoints * 3);

            HashSet<int> seen = new HashSet<int>();

            for (int i = 0; i < nPoints; i++)
            {
                float x = vertices[i * 3 + 0];
                float y = vertices[i * 3 + 1];
                float z = vertices[i * 3 + 2];

                if (x < xMin || x > xMax || y < yMin || y > yMax || z < zMin || z > zMax)
                    continue;

                byte bx = EncodeFloatToByte(x, xMin, xMax);
                byte by = EncodeFloatToByte(y, yMin, yMax);
                byte bz = EncodeFloatToByte(z, zMin, zMax);

                int key = (bx << 16) | (by << 8) | bz;

                if (!seen.Contains(key))
                {
                    seen.Add(key);

                    uniqueVertices.Add(bx);
                    uniqueVertices.Add(by);
                    uniqueVertices.Add(bz);

                    uniqueColors.Add(colors[i * 3 + 0]);
                    uniqueColors.Add(colors[i * 3 + 1]);
                    uniqueColors.Add(colors[i * 3 + 2]);
                }
            }

            try
            {
                int nUniquePoints = uniqueVertices.Count / 3;

                WriteInt(nUniquePoints);
                oSocket.GetStream().Write(uniqueVertices.ToArray(), 0, uniqueVertices.Count);
                oSocket.GetStream().Write(uniqueColors.ToArray(), 0, uniqueColors.Count);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending frame: " + ex.Message);
            }
        }

        private byte EncodeFloatToByte(float value, float min, float max)
        {
            float normalized = (value - min) / (max - min);
            int encoded = (int)(normalized * 255f);
            return (byte)(encoded < 0 ? 0 : (encoded > 255 ? 255 : encoded));
        }
    }
}
