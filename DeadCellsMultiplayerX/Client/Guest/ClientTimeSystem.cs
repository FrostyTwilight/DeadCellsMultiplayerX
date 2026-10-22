using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace DeadCellsMultiplayerX.Client.Guest
{
    internal class ClientTimeSystem
    {
        //采样
        private const int SampleCount = 8;
        private readonly List<(long rtt, long offset)> samples = new();

        //基准
        private long offsetMs;          // serverTime - localTime
        private long driftCorrection;   // 慢速修正累积
        private bool ready;

        //修正参数
        private const long HardSyncThresholdMs = 100;
        private const long MaxCorrectionPerSnapshotMs = 2;

        //公开状态
        public bool Ready => ready;

        //当前估算的服务端时间
        public long SyncedTime => NowMs() + offsetMs + driftCorrection;

        //工具
        public static long NowMs() => Stopwatch.GetTimestamp() / TimeSpan.TicksPerMillisecond;

        //NTP 采样
        /// <summary>
        /// 记录一次 NTP 往返
        /// </summary>
        /// <param name="localT0">发送请求时的本地时间</param>
        /// <param name="localT1">收到响应时的本地时间</param>
        /// <param name="serverTime">服务端返回的时间戳</param>
        public void AddSample(long localT0, long localT1, long serverTime)
        {
            long rtt = localT1 - localT0;

            // 服务端处理时刻大致对应客户端时间的"中点"
            long localMid = (localT0 + localT1) / 2;

            // offset = serverTime - localMid
            long offset = serverTime - localMid;

            samples.Add((rtt, offset));

            if (samples.Count >= SampleCount)
            {
                // 取 RTT 最小的样本
                samples.Sort((a, b) => a.rtt.CompareTo(b.rtt));
                var best = samples[0];

                offsetMs = best.offset;
                driftCorrection = 0;
                ready = true;

                samples.Clear();
            }
        }


        /// <summary>
        /// 快照驱动修正
        /// 收到快照时调用，用 ServerTime 校验并慢速修正
        /// </summary>
        public void OnSnapshot(long snapshotServerTime)
        {
            if (!ready) return;

            long predicted = SyncedTime;
            long drift = snapshotServerTime - predicted;

            if (Math.Abs(drift) > HardSyncThresholdMs)
            {
                // 漂移太大直接对齐
                offsetMs += drift;
                driftCorrection = 0;
            }
            else
            {
                // 漂移小,每次最多修 2ms
                driftCorrection += Math.Clamp(
                    drift,
                    -MaxCorrectionPerSnapshotMs,
                    MaxCorrectionPerSnapshotMs);
            }
        }
    }
}