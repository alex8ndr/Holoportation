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
        private static extern IntPtr CreateClient(int index, string ip, bool headless);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void StartClient(IntPtr handle);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void StopClient(IntPtr handle);

        [DllImport("LiveScanClient.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void DestroyClient(IntPtr handle);

        private IntPtr clientHandle;

        public NativeLiveScanClient(int index, string ip, bool headless)
        {
            clientHandle = CreateClient(index, ip, headless);
        }

        public void Start() => StartClient(clientHandle);
        public void Stop() => StopClient(clientHandle);
        public void Dispose()
        {
            Stop();
            DestroyClient(clientHandle);
            clientHandle = IntPtr.Zero;
        }
    }
}
