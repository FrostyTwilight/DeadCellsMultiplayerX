using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using Serilog;

namespace DeadCellsMultiplayerX.Server.WorldX.Hubs
{
    /// <summary>
    /// 服务端世界帧收集器
    /// 按 GUID 对客户端上传帧排序 去重 检测缺口
    /// 并通过 NACK 请求补发
    /// 每帧都是全量状态 因此缺口不影响最新状态的正确性
    /// </summary>
    internal sealed class ClientWorldFrameCollector
    {
        /// <summary>
        /// 单个客户端的接收通道
        /// </summary>
        private sealed class ClientChannel
        {
            /// <summary>
            /// 客户端 GUID
            /// </summary>
            public string Guid = string.Empty;

            /// <summary>
            /// 下一个期望的上传帧序号
            /// </summary>
            public long NextExpected;

            /// <summary>
            /// 最近已交付的帧序号
            /// </summary>
            public long LastDelivered = -1;

            /// <summary>
            /// 乱序待处理帧
            /// </summary>
            public readonly SortedDictionary<long, ClientWorldFrame> Pending = [];

            /// <summary>
            /// 最近已交付的完整帧
            /// </summary>
            public ClientWorldFrame? Latest;

            /// <summary>
            /// 上次记录的静态数据
            /// </summary>
            public HeroSpawn? LastSpawn;

            /// <summary>
            /// 静态数据版本
            /// </summary>
            public uint SpawnVersion;

            /// <summary>
            /// 缺口开始时间毫秒
            /// </summary>
            public long? GapSinceMs;

            /// <summary>
            /// 上次补发请求时间毫秒
            /// </summary>
            public long LastNackMs;

            /// <summary>
            /// 当前补发请求任务
            /// </summary>
            public Task<ClientFrameBundle>? NackTask;

            /// <summary>
            /// 补发请求发送委托
            /// </summary>
            public Func<ClientFrameRangeRequest, Task<ClientFrameBundle>>? NackSender;
        }

        /// <summary>
        /// 乱序待处理帧上限
        /// </summary>
        private const int MaxPending = 32;

        /// <summary>
        /// 单次补发请求的最大帧数
        /// </summary>
        private const int MaxNackRange = 64;

        /// <summary>
        /// 首次补发请求延迟毫秒
        /// </summary>
        private const long NackFirstDelayMs = 50;

        /// <summary>
        /// 补发请求重试间隔毫秒
        /// </summary>
        private const long NackRetryMs = 100;

        /// <summary>
        /// 缺口跳过超时毫秒
        /// </summary>
        private const long GapSkipMs = 250;

        /// <summary>
        /// 缺口跳过帧数阈值
        /// </summary>
        private const int GapSkipCount = 16;

        private static readonly ILogger Logger = Log.ForContext("SourceContext", "WorldFrameCollector");

        /// <summary>
        /// 按 GUID 索引的客户端通道
        /// </summary>
        private readonly Dictionary<string, ClientChannel> channels = [];

        /// <summary>
        /// 待广播的静态数据变化
        /// </summary>
        private readonly List<HeroSpawn> pendingSpawnChanges = [];

        /// <summary>
        /// 待广播的移除 GUID
        /// </summary>
        private readonly List<string> pendingRemoved = [];

        /// <summary>
        /// 当前世界 epoch
        /// </summary>
        public int Epoch { get; private set; }

        /// <summary>
        /// 缺口次数
        /// </summary>
        public int GapCount { get; private set; }

        /// <summary>
        /// 补发请求次数
        /// </summary>
        public int NackSent { get; private set; }

        /// <summary>
        /// 补发响应接收次数
        /// </summary>
        public int BundleReceived { get; private set; }

        /// <summary>
        /// 跳过的帧数
        /// </summary>
        public int SkippedFrames { get; private set; }

        /// <summary>
        /// 丢弃的过期帧数
        /// </summary>
        public int DroppedOld { get; private set; }

        /// <summary>
        /// 待处理帧溢出次数
        /// </summary>
        public int PendingOverflow { get; private set; }

        /// <summary>
        /// 注册客户端通道与补发委托
        /// </summary>
        public void Register(string guid, Func<ClientFrameRangeRequest, Task<ClientFrameBundle>> nackSender)
        {
            if (string.IsNullOrEmpty(guid)) return;
            GetOrCreate(guid).NackSender = nackSender;
        }

        /// <summary>
        /// 移除客户端通道并记录待广播移除
        /// </summary>
        public void Unregister(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return;
            if (channels.Remove(guid))
                pendingRemoved.Add(guid);
        }

        /// <summary>
        /// 开始新的世界 epoch
        /// </summary>
        public void BeginEpoch(int epoch)
        {
            Epoch = epoch;
            pendingSpawnChanges.Clear();
            pendingRemoved.Clear();

            foreach (var channel in channels.Values)
            {
                channel.NextExpected = 0;
                channel.LastDelivered = -1;
                channel.Pending.Clear();
                channel.Latest = null;
                channel.LastSpawn = null;
                channel.SpawnVersion = 0;
                channel.GapSinceMs = null;
                channel.LastNackMs = 0;
                channel.NackTask = null;
            }
        }

        /// <summary>
        /// 接收客户端上传帧
        /// </summary>
        public void OnFrame(string guid, ClientWorldFrame frame)
        {
            if (frame == null) return;
            if (!channels.TryGetValue(guid, out var channel)) return;

            if (frame.Epoch != Epoch)
            {
                DroppedOld++;
                return;
            }

            if (frame.Sequence <= channel.LastDelivered)
            {
                DroppedOld++;
                return;
            }

            if (frame.Sequence == channel.NextExpected)
            {
                Accept(channel, frame);
                Drain(channel);
                return;
            }

            if (channel.Pending.ContainsKey(frame.Sequence)) return;

            if (channel.Pending.Count >= MaxPending)
            {
                // 全量帧,直接跳到最新到达的帧,避免持续处于缺口状态
                PendingOverflow++;
                channel.Pending.Clear();
                channel.NextExpected = frame.Sequence;
                Accept(channel, frame);
                return;
            }

            channel.Pending[frame.Sequence] = frame;
            GapCount++;
        }

        /// <summary>
        /// 处理补发响应与缺口
        /// </summary>
        public void Tick(long nowMs)
        {
            foreach (var channel in channels.Values)
            {
                if (channel.NackTask != null && channel.NackTask.IsCompleted)
                    ConsumeBundle(channel);

                if (channel.Pending.Count == 0)
                {
                    channel.GapSinceMs = null;
                    continue;
                }

                long missingFrom = channel.NextExpected;
                long missingTo = channel.Pending.Keys.Min() - 1;
                if (missingTo < missingFrom)
                {
                    Drain(channel);
                    continue;
                }

                channel.GapSinceMs ??= nowMs;
                long gapMs = nowMs - channel.GapSinceMs.Value;
                long gapCount = missingTo - missingFrom + 1;

                if (gapMs > GapSkipMs || gapCount > GapSkipCount)
                {
                    Logger.Warning("Skip client frame gap: guid={Guid} epoch={Epoch} from={From} to={To}",
                        channel.Guid, Epoch, missingFrom, missingTo);
                    SkipToNewest(channel);
                    continue;
                }

                if (gapMs < NackFirstDelayMs) continue;
                if (channel.NackTask != null && !channel.NackTask.IsCompleted) continue;
                if (nowMs - channel.LastNackMs < NackRetryMs) continue;
                if (channel.NackSender == null) continue;

                channel.LastNackMs = nowMs;
                NackSent++;
                var request = new ClientFrameRangeRequest
                {
                    FromSequence = missingFrom,
                    ToSequence = Math.Min(missingTo, missingFrom + MaxNackRange - 1),
                };

                try
                {
                    channel.NackTask = channel.NackSender(request);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "RequestClientFrames failed: guid={Guid}", channel.Guid);
                    channel.NackTask = null;
                }
            }
        }

        /// <summary>
        /// 获取所有客户端最新的英雄动态状态
        /// </summary>
        public IReadOnlyCollection<HeroDynamic> GetActiveHeroDynamics()
        {
            var result = new List<HeroDynamic>();
            foreach (var channel in channels.Values)
            {
                var frame = channel.Latest;
                if (frame?.Heroes == null) continue;

                foreach (var hero in frame.Heroes)
                    if (hero?.Dynamic != null)
                        result.Add(CopyDynamic(hero.Dynamic));
            }
            return result;
        }

        /// <summary>
        /// 取出并清空静态数据变化
        /// </summary>
        public List<HeroSpawn> DrainSpawnChanges()
        {
            if (pendingSpawnChanges.Count == 0) return [];
            var result = pendingSpawnChanges.ToList();
            pendingSpawnChanges.Clear();
            return result;
        }

        /// <summary>
        /// 取出并清空待移除 GUID
        /// </summary>
        public List<string> DrainRemoved()
        {
            if (pendingRemoved.Count == 0) return [];
            var result = pendingRemoved.ToList();
            pendingRemoved.Clear();
            return result;
        }

        /// <summary>
        /// 获取所有客户端当前静态数据
        /// </summary>
        public List<HeroSpawn> GetAllSpawns()
        {
            var result = new List<HeroSpawn>();
            foreach (var channel in channels.Values)
                if (channel.LastSpawn != null)
                    result.Add(channel.LastSpawn);
            return result;
        }

        /// <summary>
        /// 清空收集器
        /// </summary>
        public void Clear()
        {
            channels.Clear();
            pendingSpawnChanges.Clear();
            pendingRemoved.Clear();
        }

        /// <summary>
        /// 处理补发响应任务
        /// </summary>
        private void ConsumeBundle(ClientChannel channel)
        {
            var task = channel.NackTask!;
            if (task.IsFaulted)
            {
                Logger.Warning(task.Exception, "RequestClientFrames faulted: guid={Guid}", channel.Guid);
                channel.NackTask = null;
                return;
            }
            if (task.IsCanceled)
            {
                channel.NackTask = null;
                return;
            }
            if (!task.IsCompletedSuccessfully) return;

            BundleReceived++;
            channel.NackTask = null;

            var bundle = task.Result;
            if (bundle == null) return;

            if (bundle.Frames != null)
                foreach (var frame in bundle.Frames)
                    if (frame != null)
                        OnFrame(channel.Guid, frame);

            if (bundle.Unavailable && channel.Pending.Count > 0)
                SkipToNewest(channel);
        }

        /// <summary>
        /// 接受连续帧并更新最新状态
        /// </summary>
        private void Accept(ClientChannel channel, ClientWorldFrame frame)
        {
            channel.LastDelivered = frame.Sequence;
            channel.NextExpected = frame.Sequence + 1;
            channel.Latest = frame;
            channel.GapSinceMs = null;

            var upload = frame.Heroes?.FirstOrDefault(h => h?.Dynamic != null);
            if (upload == null) return;

            if (upload.Spawn != null)
            {
                if (channel.LastSpawn == null || !upload.Spawn.SameAs(channel.LastSpawn))
                {
                    channel.SpawnVersion++;
                    upload.Spawn.Version = channel.SpawnVersion;
                    channel.LastSpawn = upload.Spawn;
                    pendingSpawnChanges.Add(upload.Spawn);
                }
            }

            upload.Dynamic.SpawnVersion = channel.SpawnVersion;
        }

        /// <summary>
        /// 排空连续待处理帧
        /// </summary>
        private void Drain(ClientChannel channel)
        {
            while (channel.Pending.Remove(channel.NextExpected, out var next))
                Accept(channel, next);
        }

        /// <summary>
        /// 跳到最新待处理帧
        /// </summary>
        private void SkipToNewest(ClientChannel channel)
        {
            if (channel.Pending.Count == 0)
            {
                channel.GapSinceMs = null;
                return;
            }

            long newest = channel.Pending.Keys.Max();
            long skipped = newest - channel.NextExpected;
            if (skipped > 0)
                SkippedFrames += (int)skipped;

            channel.Pending.Remove(newest, out var frame);
            channel.Pending.Clear();
            channel.NextExpected = newest;

            if (frame != null)
                Accept(channel, frame);

            channel.GapSinceMs = null;
        }

        /// <summary>
        /// 复制动态状态 避免历史帧与收集器共享可变字段
        /// </summary>
        private static HeroDynamic CopyDynamic(HeroDynamic source)
        {
            return new HeroDynamic
            {
                GUID = source.GUID,
                ChangeMask = source.ChangeMask,
                Pos = source.Pos,
                Anim = source.Anim,
                Affects = source.Affects,
                Flags = source.Flags,
                LevelId = source.LevelId,
                SubLevelIndex = source.SubLevelIndex,
                SpawnVersion = source.SpawnVersion,
            };
        }

        /// <summary>
        /// 获取或创建客户端通道
        /// </summary>
        private ClientChannel GetOrCreate(string guid)
        {
            if (!channels.TryGetValue(guid, out var channel))
            {
                channel = new ClientChannel { Guid = guid };
                channels[guid] = channel;
            }
            return channel;
        }
    }
}
