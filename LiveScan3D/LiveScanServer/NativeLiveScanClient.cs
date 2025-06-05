using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KinectServer
{
    public class NativeLiveScanClient
    {
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

        private IntPtr clientHandle;

        public NativeLiveScanClient(int index, string ip)
        {
            clientHandle = CreateClient(index, ip);
        }

        public void Start() => StartClient(clientHandle);
        public void Stop() => StopClient(clientHandle);
        public void Dispose()
        {
            Stop();
            DestroyClient(clientHandle);
            clientHandle = IntPtr.Zero;
        }

        public void StartFrameCapture() => StartFrameCapture(clientHandle);
        public void Calibrate() => Calibrate(clientHandle);

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

        public void ReceiveCalibration(AffineTransform transform)
        {
            var native = transform.ToNative();
            ReceiveCalibration(clientHandle, ref native);
        }

        public void ClearStoredFrames() => ClearStoredFrames(clientHandle);

        public void EnableTemporalSync(int syncOffset) => EnableTemporalSync(clientHandle, syncOffset);

        public void DisableTemporalSync() => DisableTemporalSync(clientHandle);

        public void StartMaster() => StartMaster(clientHandle);

        public void RequestSyncJackState() => RequestSyncJackState(clientHandle);
    }
}
