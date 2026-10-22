using System;
using System.Collections.Generic;
using dc.en;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Utils;

namespace DeadCellsMultiplayerX.Client.Guest.WorldX.Snapshot
{
    /// <summary>
    /// 客户端发送缓冲
    /// 每帧采集本地世界状态
    /// 供服务端 NACK 补发
    /// </summary>
    internal sealed class ClientWorldSendBuffer
    {
        /// <summary>
        /// 缓冲最大存储值
        /// </summary>
        private const int MaxHistory = 256;

        /// <summary>
        ///定期重发静态数据,避免服务端跳缺口后永久缺失
        /// </summary>
        private const int SpawnRefreshFrames = 60;

        /// <summary>
        /// 缓冲数组
        /// 泛型:<see cref="ClientWorldFrame"/>
        /// </summary>
        private readonly List<ClientWorldFrame> history = [];

        /// <summary>
        /// 发送历史中最早一帧的序号
        /// 历史为空时无意义
        /// </summary>
        private long firstSequence;

        /// <summary>
        /// 最近一次发送的帧序号
        /// 初始为 -1
        /// </summary>
        private long lastSequence = -1;

        /// <summary>
        /// 客户端本地帧号
        /// 每构建一帧自增
        /// </summary>
        private long frameId;

        /// <summary>
        /// 上一次随帧发送的静态数据
        /// 用于判断静态数据是否变化
        /// </summary>
        private HeroSpawn? lastSpawn;

        /// <summary>
        /// 距离上次携带静态数据的帧数
        /// 达到阈值后强制重发
        /// </summary>
        private int framesSinceSpawn;

        /// <summary>
        /// 当前帧采集到的静态数据
        /// </summary>
        private HeroSpawn? capturedSpawn;

        /// <summary>
        /// 当前帧采集到的动态数据
        /// </summary>
        private HeroDynamic? capturedDynamic;

        /// <summary>
        /// 当前帧采集时的同步时间(毫秒)
        /// </summary>
        private long capturedTimeMs;

        /// <summary>
        /// 是否存在尚未发送的采集结果
        /// </summary>
        private bool hasCapture;

        /// <summary>
        /// 当前回显的服务端世界 epoch
        /// </summary>
        public int Epoch { get; private set; }

        /// <summary>
        /// 最近一次发送的帧序号
        /// </summary>
        public long LastSequence => lastSequence;

        /// <summary>
        /// 每帧采集当前本地英雄状态,失败时下一帧会重试
        /// </summary>
        /// <param name="hero"></param>
        /// <param name="guid">客户guid</param>
        /// <param name="syncedTimeMs">同步时间</param>
        /// <returns></returns>
        public bool Capture(Hero hero, string guid, long syncedTimeMs)
        {
            if (hero == null || hero.destroyed || !hero.initDone || hero.spr == null)
                return false;

            var (spawn, dyn) = HeroUtils.Collect(hero, guid, syncedTimeMs);
            capturedSpawn = spawn;
            capturedDynamic = dyn;
            capturedTimeMs = syncedTimeMs;
            hasCapture = true;
            return true;
        }

        /// <summary>
        /// 用最近一次采集构建世界帧,分配序号并写入发送历史
        /// </summary>
        public ClientWorldFrame? BuildFrameAndRecord()
        {
            if (!hasCapture || capturedDynamic == null) return null;

            long sequence = lastSequence + 1;
            long frame = ++frameId;

            var upload = new HeroUpload
            {
                ClientTime = capturedTimeMs,
                FrameId = (uint)frame,
                Dynamic = capturedDynamic,
            };

            // 静态数据在首次、变化时附带,并定期重发作为关键帧
            framesSinceSpawn++;
            if (capturedSpawn != null
                && (lastSpawn == null
                    || !capturedSpawn.SameAs(lastSpawn)
                    || framesSinceSpawn >= SpawnRefreshFrames))
            {
                upload.Spawn = capturedSpawn;
                lastSpawn = capturedSpawn;
                framesSinceSpawn = 0;
            }

            var result = new ClientWorldFrame
            {
                Epoch = Epoch,
                Sequence = sequence,
                ClientTimeMs = capturedTimeMs,
                FrameId = frame,
                Heroes = [upload],
            };

            history.Add(result);
            if (history.Count == 1)
                firstSequence = sequence;
            lastSequence = sequence;

            if (history.Count > MaxHistory)
            {
                history.RemoveRange(0, history.Count - MaxHistory);
                firstSequence = history[0].Sequence;
            }

            hasCapture = false;
            capturedSpawn = null;
            capturedDynamic = null;
            return result;
        }

        /// <summary>
        /// 按序号返回可补发的帧;历史已淘汰时标记 Unavailable
        /// </summary>
        /// <param name="request">帧数据</param>
        /// <returns><see cref="ClientFrameBundle"/></returns>
        public ClientFrameBundle BuildBundle(ClientFrameRangeRequest request)
        {
            var bundle = new ClientFrameBundle();
            if (request == null || request.ToSequence < request.FromSequence)
            {
                bundle.Unavailable = true;
                return bundle;
            }

            if (history.Count == 0
                || request.FromSequence < firstSequence
                || request.ToSequence > lastSequence)
            {
                bundle.Unavailable = true;
            }

            long start = Math.Max(request.FromSequence, firstSequence);
            long end = Math.Min(request.ToSequence, lastSequence);
            for (long seq = start; seq <= end; seq++)
            {
                int index = (int)(seq - firstSequence);
                if (index >= 0 && index < history.Count)
                    bundle.Frames.Add(history[index]);
            }

            return bundle;
        }

        /// <summary>
        /// 观测到更大的服务端 epoch:清空历史并重置序号
        /// </summary>
        /// <param name="epoch"></param>
        public void SetEpoch(int epoch)
        {
            if (epoch <= Epoch) return;

            Epoch = epoch;
            history.Clear();
            firstSequence = 0;
            lastSequence = -1;
            frameId = 0;
            lastSpawn = null;
            framesSinceSpawn = 0;
            ClearCapture();
        }

        /// <summary>
        /// 本地英雄重新初始化
        /// </summary>
        /// <remarks>用于下一帧强制携带静态数据</remarks>
        public void ResetLocalHeroState()
        {
            lastSpawn = null;
            framesSinceSpawn = 0;
            ClearCapture();
        }

        public void Clear()
        {
            history.Clear();
            firstSequence = 0;
            lastSequence = -1;
            frameId = 0;
            lastSpawn = null;
            framesSinceSpawn = 0;
            ClearCapture();
        }

        private void ClearCapture()
        {
            hasCapture = false;
            capturedSpawn = null;
            capturedDynamic = null;
        }
    }
}
