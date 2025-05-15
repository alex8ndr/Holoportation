using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;

namespace Assets.Scripts.Utils
{
    public class PlayerDataChannel
    {
        public DataChannel Channel;
        public bool CanSend;
        public ConcurrentQueue<byte[]> DataQueue;
    }
}
