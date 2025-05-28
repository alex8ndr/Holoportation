using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace KinectServer
{
    public class TransferSocket
    {
        TcpClient oSocket;

        private const float PRECISION = 0.0025f;
        private const float MAX_RANGE = PRECISION * 255;

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
            int nPoints = vertices.Count / 3;

            // Find actual min/max for each axis
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < nPoints; i++)
            {
                float x = vertices[i * 3 + 0];
                float y = vertices[i * 3 + 1];
                float z = vertices[i * 3 + 2];

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;

                if (y < minY) minY = y;
                if (y > maxY) maxY = y;

                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }

            // Clamp dynamic range to acceptable bounds
            AdjustRange(ref minX, ref maxX);
            AdjustRange(ref minY, ref maxY);
            AdjustRange(ref minZ, ref maxZ);

            // Encode min/max as bytes (centimeter precision over 1m)
            byte minXb = EncodeFloatToByte(minX, 0, 1);
            byte maxXb = EncodeFloatToByte(maxX, 0, 1);
            byte minYb = EncodeFloatToByte(minY, 0, 1);
            byte maxYb = EncodeFloatToByte(maxY, 0, 1);
            byte minZb = EncodeFloatToByte(minZ, 0, 1);
            byte maxZb = EncodeFloatToByte(maxZ, 0, 1);

            float xMin = DecodeByteToFloat(minXb, 0, 1);
            float xMax = DecodeByteToFloat(maxXb, 0, 1);
            float yMin = DecodeByteToFloat(minYb, 0, 1);
            float yMax = DecodeByteToFloat(maxYb, 0, 1);
            float zMin = DecodeByteToFloat(minZb, 0, 1);
            float zMax = DecodeByteToFloat(maxZb, 0, 1);

            // Encode with deduplication
            List<byte> uniqueVertices = new List<byte>(nPoints * 3);
            List<byte> uniqueColors = new List<byte>(nPoints * 3);
            HashSet<int> seen = new HashSet<int>();

            for (int i = 0; i < nPoints; i++)
            {
                float x = vertices[i * 3 + 0];
                float y = vertices[i * 3 + 1];
                float z = vertices[i * 3 + 2];

                if (x < minX || x > maxX || y < minY || y > maxY || z < minZ || z > maxZ)
                    continue;

                byte bx = EncodeFloatToByte(x, minX, maxX);
                byte by = EncodeFloatToByte(y, minY, maxY);
                byte bz = EncodeFloatToByte(z, minZ, maxZ);

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

                // Prepare the full payload
                byte[] header = new byte[10]; // 4 bytes for int, 6 for min/max

                // Write point count (4 bytes, little endian)
                BitConverter.GetBytes(nUniquePoints).CopyTo(header, 0);

                // Write min/max as bytes
                header[4] = minXb; header[5] = maxXb;
                header[6] = minYb; header[7] = maxYb;
                header[8] = minZb; header[9] = maxZb;

                // Concatenate all buffers
                byte[] payload = new byte[header.Length + uniqueVertices.Count + uniqueColors.Count];
                Buffer.BlockCopy(header, 0, payload, 0, header.Length);
                Buffer.BlockCopy(uniqueVertices.ToArray(), 0, payload, header.Length, uniqueVertices.Count);
                Buffer.BlockCopy(uniqueColors.ToArray(), 0, payload, header.Length + uniqueVertices.Count, uniqueColors.Count);

                oSocket.GetStream().Write(payload, 0, payload.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending frame: " + ex.Message);
            }
        }

        private void AdjustRange(ref float min, ref float max)
        {
            float center = (min + max) / 2f;
            min = center - (MAX_RANGE / 2f);
            max = center + (MAX_RANGE / 2f);
        }

        private byte EncodeFloatToByte(float value, float min, float max)
        {
            float normalized = (value - min) / (max - min);
            int encoded = (int)(normalized * 255f);
            return (byte)(encoded < 0 ? 0 : (encoded > 255 ? 255 : encoded));
        }

        private float DecodeByteToFloat(byte b, float min, float max)
        {
            return (b / 255f) * (max - min) + min;
        }
    }
}
