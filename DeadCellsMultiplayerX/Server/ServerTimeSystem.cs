using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace DeadCellsMultiplayerX.Server
{
    internal class ServerTimeSystem
    {
        private readonly Stopwatch stopwatch = new();
        private long prevMs;    //上一个时间戳
        public long Now { get; private set; }   //当前服务端时间

        public void Start()
        {
            stopwatch.Restart();
            prevMs = 0;
            Now = 0;
        }

        //每帧调用
        public void Tick()
        {
            long now = stopwatch.ElapsedMilliseconds;
            Now += now - prevMs;
            prevMs = now;
        }
    }
}