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
            float halfRange = 128 * precision;

            int nPoints = vertices.Count / 3;

            // Will store unique quantized points and corresponding color bytes
            List<byte> uniqueVertices = new List<byte>(nPoints * 3);
            List<byte> uniqueColors = new List<byte>(nPoints * 3);

            // Use HashSet to store packed 3-byte position keys (e.g., x|y|z as int)
            HashSet<int> seen = new HashSet<int>();

            for (int i = 0; i < nPoints; i++)
            {
                float x = vertices[i * 3 + 0];
                float y = vertices[i * 3 + 1];
                float z = vertices[i * 3 + 2];

                if (Math.Abs(x) > halfRange || Math.Abs(y) > halfRange || Math.Abs(z) > halfRange)
                    continue;

                byte bx = EncodeFloatToByte(x, precision, halfRange);
                byte by = EncodeFloatToByte(y, precision, halfRange);
                byte bz = EncodeFloatToByte(z, precision, halfRange);

                // Pack x/y/z bytes into a single int key (avoiding collisions)
                int key = (bx << 16) | (by << 8) | bz;

                if (!seen.Contains(key))
                {
                    seen.Add(key);

                    uniqueVertices.Add(bx);
                    uniqueVertices.Add(by);
                    uniqueVertices.Add(bz);

                    // Add corresponding color
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

        private byte EncodeFloatToByte(float value, float precision, float halfRange)
        {
            return (byte)((value + halfRange) / (2f * halfRange) * 255f);
        }
    }
}
