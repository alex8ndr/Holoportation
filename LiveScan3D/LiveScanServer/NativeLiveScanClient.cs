using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static KinectServer.KinectSocket;

namespace KinectServer
{
    public class NativeLiveScanClient
    {
        /*
         * Server to client (outbound) calls
         */
        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr CreateClient(int index, string ip);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void StartClient(IntPtr handle);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void StopClient(IntPtr handle);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void DestroyClient(IntPtr handle);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void StartFrameCapture(IntPtr handle);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Calibrate(IntPtr handle);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetSettings(IntPtr handle, ref NativeKinectSettings settings);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RequestStoredFrame(IntPtr handle);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RequestLastFrame(IntPtr handle);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ReceiveCalibration(IntPtr handle, ref NativeAffineTransform calibration);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ClearStoredFrames(IntPtr handle);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void EnableTemporalSync(IntPtr handle, int syncOffset);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void DisableTemporalSync(IntPtr handle);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void StartMaster(IntPtr handle);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RequestSyncJackState(IntPtr handle);

        /*
         * Client to server (inbound) calls
         */
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ConfirmCapturedCallback(int clientIndex);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate void ConfirmCalibratedCallback(int clientIndex, int markerId, float* R, float* t);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate void SendLatestFrameCallback(int clientIndex, Point3s* vertices, RGB* colors, int count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate void SendStoredFrameCallback(int clientIndex, Point3s* vertices, RGB* colors, int count, byte noMoreFrames);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ConfirmTempSyncStateCallback(int clientIndex, int tempSyncState);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ConfirmMasterRestartCallback(int clientIndex);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SendDeviceSyncStateCallback(int clientIndex, int tempSyncState);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetConfirmCapturedCallback(IntPtr handle, ConfirmCapturedCallback cb);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetConfirmCalibratedCallback(IntPtr handle, ConfirmCalibratedCallback cb);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetSendLatestFrameCallback(IntPtr handle, SendLatestFrameCallback callback);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetSendStoredFrameCallback(IntPtr handle, SendStoredFrameCallback callback);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetConfirmTempSyncStateCallback(IntPtr handle, ConfirmTempSyncStateCallback callback);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetConfirmMasterRestartCallback(IntPtr handle, ConfirmMasterRestartCallback callback);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetSendDeviceSyncStateCallback(IntPtr handle, SendDeviceSyncStateCallback callback);

        public int clientIndex;
        public bool bFrameCaptured = false;
        public bool bCalibrated = false;
        public bool bLatestFrameReceived = false;
        public bool bStoredFrameReceived = false;
        public bool bNoMoreStoredFrames = true;
        public bool bSubStarted = false;
        public bool bMasterStarted = false;

        public enum eTempSyncConfig { MASTER, SUBORDINATE, STANDALONE, UNKNOWN }

        //Shows how the *Device* is configured (Determined only by the Sync-Cables hooked up to the device)
        public eTempSyncConfig currentDeviceTempSyncState = eTempSyncConfig.STANDALONE;

        //Shows how the Client-Software is configured (This is set by the server)
        public eTempSyncConfig currentClientTempSyncState = eTempSyncConfig.STANDALONE;

        //The pose of the sensor in the scene (used by the OpenGLWindow to show the sensor)
        public AffineTransform oCameraPose = new AffineTransform();

        //The transform that maps the vertices in the sensor coordinate system to the world coordinate system.
        public AffineTransform oWorldTransform = new AffineTransform();

        public string sSocketState;

        public List<byte> lFrameRGB = new List<byte>();
        public List<float> lFrameVerts = new List<float>();

        private IntPtr clientHandle;
        private ConfirmCapturedCallback confirmCapturedCallback;
        private ConfirmCalibratedCallback confirmCalibratedCallback;
        private SendLatestFrameCallback sendLatestFrameCallback;
        private SendStoredFrameCallback sendStoredFrameCallback;
        private ConfirmTempSyncStateCallback confirmTempSyncStateCallback;
        private ConfirmMasterRestartCallback confirmMasterRestartCallback;
        private SendDeviceSyncStateCallback sendDeviceSyncStateCallback;

        public NativeLiveScanClient(int index, string ip)
        {
            clientHandle = CreateClient(index, ip);
            clientIndex = index;
            sSocketState = "[Client " + clientIndex.ToString() + "] Calibrated = false";

            UpdateSocketState();
        }

        public void Start() => StartClient(clientHandle);
        public void Stop() => StopClient(clientHandle);
        public void Dispose()
        {
            Stop();
            DestroyClient(clientHandle);
            clientHandle = IntPtr.Zero;
        }

        public void StartFrameCapture()
        {
            bFrameCaptured = false;
            StartFrameCapture(clientHandle);
        }

        public void Calibrate()
        {
            bCalibrated = false;
            UpdateSocketState();

            Calibrate(clientHandle);
        }

        public void SetSettings(KinectSettings settings)
        {
            var native = settings.ToNative(out GCHandle markerHandle);

            try
            {
                SetSettings(clientHandle, ref native);
            }
            finally
            {
                if (markerHandle.IsAllocated)
                    markerHandle.Free();
            }
        }

        public void RequestStoredFrame() => RequestStoredFrame(clientHandle);
        public void RequestLastFrame() => RequestLastFrame(clientHandle);

        public void ReceiveCalibration()
        {
            var native = oWorldTransform.ToNative();
            ReceiveCalibration(clientHandle, ref native);
        }

        public void ClearStoredFrames() => ClearStoredFrames(clientHandle);

        public void EnableTemporalSync(int syncOffset)
        {
            bSubStarted = false;
            currentClientTempSyncState = eTempSyncConfig.UNKNOWN;
            EnableTemporalSync(clientHandle, syncOffset);
        }

        public void DisableTemporalSync()
        {
            bSubStarted = false;
            currentClientTempSyncState = eTempSyncConfig.UNKNOWN;
            DisableTemporalSync(clientHandle);
        }

        public void StartMaster() => StartMaster(clientHandle);

        public void RequestSyncJackState() => RequestSyncJackState(clientHandle);

        public void SetConfirmCapturedCallback(Action<int> callback)
        {
            confirmCapturedCallback = new ConfirmCapturedCallback(callback);
            SetConfirmCapturedCallback(clientHandle, confirmCapturedCallback);
        }

        public unsafe void SetConfirmCalibratedCallback(Action<int, int, float[], float[]> callback)
        {
            confirmCalibratedCallback = new ConfirmCalibratedCallback((clientIndex, markerId, R, t) =>
            {
                float[] rotation = new float[9];
                float[] translation = new float[3];
                Marshal.Copy((IntPtr)R, rotation, 0, 9);
                Marshal.Copy((IntPtr)t, translation, 0, 3);
                callback(clientIndex, markerId, rotation, translation);
            });

            SetConfirmCalibratedCallback(clientHandle, confirmCalibratedCallback);
        }

        public unsafe void SetSendLatestFrameCallback()
        {
            sendLatestFrameCallback = new SendLatestFrameCallback((int index, Point3s* vertices, RGB* colors, int count) =>
            {
                // Ensure list capacity
                if (lFrameVerts.Capacity < count * 3)
                    lFrameVerts.Capacity = count * 3;
                if (lFrameRGB.Capacity < count * 3)
                    lFrameRGB.Capacity = count * 3;

                lFrameVerts.Clear();
                lFrameRGB.Clear();

                for (int i = 0; i < count; i++)
                {
                    lFrameVerts.Add(vertices[i].X / 1000.0f);
                    lFrameVerts.Add(vertices[i].Y / 1000.0f);
                    lFrameVerts.Add(vertices[i].Z / 1000.0f);

                    lFrameRGB.Add(colors[i].rgbRed);
                    lFrameRGB.Add(colors[i].rgbGreen);
                    lFrameRGB.Add(colors[i].rgbBlue);
                }

                bLatestFrameReceived = true;
            });

            SetSendLatestFrameCallback(clientHandle, sendLatestFrameCallback);
        }

        public unsafe void SetSendStoredFrameCallback()
        {
            sendStoredFrameCallback = new SendStoredFrameCallback((int index, Point3s* vertices, RGB* colors, int count, byte noMoreFrames) =>
            {
                if (noMoreFrames != 0)
                {
                    bNoMoreStoredFrames = true;
                    bStoredFrameReceived = true;
                    return;
                }

                // Ensure list capacity
                if (lFrameVerts.Capacity < count * 3)
                    lFrameVerts.Capacity = count * 3;
                if (lFrameRGB.Capacity < count * 3)
                    lFrameRGB.Capacity = count * 3;

                lFrameVerts.Clear();
                lFrameRGB.Clear();

                for (int i = 0; i < count; i++)
                {
                    lFrameVerts.Add(vertices[i].X / 1000.0f);
                    lFrameVerts.Add(vertices[i].Y / 1000.0f);
                    lFrameVerts.Add(vertices[i].Z / 1000.0f);

                    lFrameRGB.Add(colors[i].rgbRed);
                    lFrameRGB.Add(colors[i].rgbGreen);
                    lFrameRGB.Add(colors[i].rgbBlue);
                }

                bStoredFrameReceived = true;
            });

            SetSendStoredFrameCallback(clientHandle, sendStoredFrameCallback);
        }

        public void SetConfirmTempSyncStateCallback(Action<int, eTempSyncConfig> callback)
        {
            confirmTempSyncStateCallback = new ConfirmTempSyncStateCallback((int index, int state) =>
            {
               eTempSyncConfig config = eTempSyncConfig.UNKNOWN;

               switch (state)
               {
                    case 0:
                        config = eTempSyncConfig.SUBORDINATE;
                        break;
                    case 1:
                        config = eTempSyncConfig.MASTER;
                        break;
                    case 2:
                        config = eTempSyncConfig.STANDALONE;
                        break;
                    default:
                        config = eTempSyncConfig.UNKNOWN;
                        break;
               }

                callback(index, config);
            });

            SetConfirmTempSyncStateCallback(clientHandle, confirmTempSyncStateCallback);
        }

        public void SetConfirmMasterRestartCallback(Action<int> callback)
        {
            confirmMasterRestartCallback = new ConfirmMasterRestartCallback((int index) =>
            {
                bMasterStarted = true;
                callback(index);
            });

            SetConfirmTempSyncStateCallback(clientHandle, confirmTempSyncStateCallback);
        }

        public void SetSendDeviceSyncStateCallback(Action<int> callback)
        {
            sendDeviceSyncStateCallback = new SendDeviceSyncStateCallback((int index, int state) =>
            {
                switch (state)
                {
                    case 0:
                        currentDeviceTempSyncState = eTempSyncConfig.SUBORDINATE;
                        break;
                    case 1:
                        currentDeviceTempSyncState = eTempSyncConfig.MASTER;
                        break;
                    case 2:
                        currentDeviceTempSyncState = eTempSyncConfig.STANDALONE;
                        break;
                    default:
                        currentDeviceTempSyncState = eTempSyncConfig.UNKNOWN;
                        break;
                }

                callback(index);
            });

            SetSendDeviceSyncStateCallback(clientHandle, sendDeviceSyncStateCallback);
        }

        public void UpdateSocketState()
        {
            string tempSyncMessage = "";

            switch (currentClientTempSyncState)
            {
                case eTempSyncConfig.MASTER:
                    tempSyncMessage = "[MASTER]";
                    break;
                case eTempSyncConfig.SUBORDINATE:
                    tempSyncMessage = "[SUBORDINATE]";
                    break;
                default:
                    break;
            }

            sSocketState = "[Client " + clientIndex.ToString() + "] Calibrated = " + bCalibrated + " " + tempSyncMessage;
        }
    }
}
