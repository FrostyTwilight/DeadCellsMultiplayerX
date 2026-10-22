using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsSync.Core.Snapshot;
using Serilog;

namespace DeadCellsMultiplayerX.Client.Guest.WorldX.Snapshot
{
    /// <summary>
    /// 客户端世界帧接收管理器
    /// 负责 epoch 校验 乱序重排 缺口补发
    /// 并把连续帧写入唯一的渲染时间线
    /// </summary>
    internal sealed class WorldFrameManager
    {
        /// <summary>
        /// 渲染插值延迟秒数
        /// </summary>
        private const double InterpolationDelaySeconds = 0.1;

        /// <summary>
        /// 乱序待处理帧上限
        /// </summary>
        private const int MaxPending = 64;

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

        /// <summary>
        /// 重同步请求重试间隔毫秒
        /// </summary>
        private const long ResyncRetryMs = 1000;

        private static readonly ILogger Logger = Log.ForContext("SourceContext", "WorldFrameManager");

        /// <summary>
        /// 客户端会话
        /// </summary>
        private readonly GuestClientSession session;

        /// <summary>
        /// 渲染时间线
        /// </summary>
        private readonly SnapshotBuffer<WorldFrame> timeline = new(InterpolationDelaySeconds);

        /// <summary>
        /// 乱序待处理帧
        /// </summary>
        private readonly SortedDictionary<long, WorldFrame> pending = [];

        /// <summary>
        /// 英雄静态数据缓存
        /// </summary>
        private readonly Dictionary<string, HeroSpawn> spawns = [];

        /// <summary>
        /// 下一个期望的帧序号
        /// </summary>
        private long nextExpected;

        /// <summary>
        /// 缺口开始时间毫秒
        /// </summary>
        private long? gapSinceMs;

        /// <summary>
        /// 上次补发请求时间毫秒
        /// </summary>
        private long lastNackMs;

        /// <summary>
        /// 上次重同步请求本地时间毫秒
        /// </summary>
        private long lastResyncRequestLocalMs;

        /// <summary>
        /// 当前补发请求任务
        /// </summary>
        private Task<WorldFrameBundle>? nackTask;

        /// <summary>
        /// 当前重同步请求任务
        /// </summary>
        private Task<WorldResync>? resyncTask;

        /// <summary>
        /// 是否已建立渲染基线
        /// </summary>
        private bool hasBaseline;

        /// <summary>
        /// 是否已记录首帧日志
        /// </summary>
        private bool loggedFirstFrame;


        public WorldFrameManager(GuestClientSession session, string localGuid)
        {
            this.session = session;

#if DEBUG
            WorldFrameDebug.ClientManager = this;
#endif
        }

        /// <summary>
        /// 当前服务端世界 epoch
        /// </summary>
        public int Epoch { get; private set; } = -1;

        /// <summary>
        /// 最近已交付的帧序号
        /// </summary>
        public long LastDeliveredSequence { get; private set; } = -1;

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
        /// 重同步请求次数
        /// </summary>
        public int ResyncCount { get; private set; }

        /// <summary>
        /// 丢弃的过期帧数
        /// </summary>
        public int DroppedOld { get; private set; }

        /// <summary>
        /// 待处理帧溢出次数
        /// </summary>
        public int PendingOverflow { get; private set; }

        /// <summary>
        /// 待处理帧数量
        /// </summary>
        public int PendingCount => pending.Count;

        /// <summary>
        /// 是否已建立渲染基线
        /// </summary>
        public bool HasBaseline => hasBaseline;

        /// <summary>
        /// 连续帧写入时间线后触发
        /// </summary>
        public event Action<WorldFrame>? FrameApplied;

        /// <summary>
        /// 请求初始世界基线
        /// </summary>
        public void Start()
        {
            RequestResync("initial");
        }

        /// <summary>
        /// 服务端广播入口
        /// </summary>
        public void OnFrame(WorldFrame frame)
        {
            if (frame == null) return;

            // 首次收到世界帧 先记录 epoch 并请求基线
            if (Epoch < 0)
            {
                Epoch = frame.Epoch;
                session.ObserveWorldEpoch(Epoch);
                RequestResync("initial");
            }
            // 服务端世界重载 清空旧状态并重新建立基线
            else if (frame.Epoch > Epoch)
            {
                Reset();
                Epoch = frame.Epoch;
                session.ObserveWorldEpoch(Epoch);
                RequestResync("epoch changed");
            }
            // 旧 epoch 的迟到帧直接丢弃
            else if (frame.Epoch < Epoch)
            {
                DroppedOld++;
                return;
            }

            // 用最新服务端时间校正本地时钟
            session.ObserveServerTime((long)frame.ServerTimeMs);

            // 基线建立前先缓存 不能直接进入时间线
            if (!hasBaseline)
            {
                BufferPending(frame);
                return;
            }

            // 重复帧或已交付的旧帧
            if (frame.Sequence <= LastDeliveredSequence
                || pending.ContainsKey(frame.Sequence))
            {
                DroppedOld++;
                return;
            }

            // 连续帧 立即应用并继续排空缓存
            if (frame.Sequence == nextExpected)
            {
                ApplyFrame(frame);
                DrainContiguous();
                return;
            }

            // 乱序帧先缓存 等 Tick 调度补发
            BufferPending(frame);
            GapCount++;
        }

        /// <summary>
        /// 处理补发响应
        /// </summary>
        public void OnFrameBundle(WorldFrameBundle bundle)
        {
            if (bundle == null) return;

            BundleReceived++;
            // 服务端历史帧已淘汰 直接用全量基线覆盖
            if (bundle.Resync != null)
            {
                OnResync(bundle.Resync);
                return;
            }

            if (bundle.Frames == null) return;
            // 补发帧仍走正常排序与去重流程
            foreach (var frame in bundle.Frames)
                OnFrame(frame);
        }

        /// <summary>
        /// 应用全量世界基线
        /// </summary>
        public void OnResync(WorldResync resync)
        {
            if (resync == null) return;
            if (resync.Epoch < Epoch)
            {
                DroppedOld++;
                return;
            }

            // 迟到的重同步响应不应把已前进的时间线回退
            if (hasBaseline && resync.Epoch == Epoch
                && resync.Sequence < LastDeliveredSequence)
            {
                DroppedOld++;
                return;
            }

            // 更高 epoch 说明世界已重载 先整体重置
            if (resync.Epoch > Epoch)
            {
                Reset();
                Epoch = resync.Epoch;
            }

            session.ObserveWorldEpoch(resync.Epoch);
            session.ObserveServerTime((long)resync.ServerTimeMs);

            // 基线覆盖当前状态 清空旧时间线与缓存
            timeline.Clear();
            pending.Clear();
            spawns.Clear();
            hasBaseline = true;
            gapSinceMs = null;

            if (resync.Spawns != null)
            {
                foreach (var spawn in resync.Spawns)
                    if (spawn != null)
                        spawns[spawn.GUID] = spawn;
            }

            // 将基线包装成普通世界帧 统一走 ApplyFrame
            var baseline = new WorldFrame
            {
                Epoch = resync.Epoch,
                Sequence = resync.Sequence,
                ServerTimeMs = resync.ServerTimeMs,
                Heroes = resync.Heroes ?? [],
                Spawned = resync.Spawns ?? [],
                Despawned = [],
            };

            ApplyFrame(baseline);

            Logger.Information(
                "World resync applied: epoch={Epoch} seq={Sequence} heroes={Heroes}",
                resync.Epoch, resync.Sequence, baseline.Heroes.Count);
        }

        /// <summary>
        /// 处理重同步 补发与缺口
        /// </summary>
        public void Tick(long nowMs)
        {
#if DEBUG
            // 调试命令强制重同步
            if (WorldFrameDebug.ConsumeForceResync())
            {
                resyncTask = null;
                lastResyncRequestLocalMs = 0;
                RequestResync("debug");
            }
#endif

            // 轮询异步请求结果 避免阻塞游戏线程
            PollResync();
            PollNack();

            // 基线未建立时定期重试
            if (!hasBaseline)
            {
                if (Environment.TickCount64 - lastResyncRequestLocalMs >= ResyncRetryMs)
                    RequestResync("baseline retry");
                return;
            }

            // 没有乱序帧说明缺口已补齐
            if (pending.Count == 0)
            {
                gapSinceMs = null;
                return;
            }

            // 计算缺失的序号区间
            long missingFrom = nextExpected;
            long missingTo = pending.Keys.Min() - 1;
            if (missingTo < missingFrom)
            {
                DrainContiguous();
                return;
            }

            // 记录缺口首次出现时间 用于超时判断
            gapSinceMs ??= nowMs;
            long gapMs = nowMs - gapSinceMs.Value;
            long gapCount = missingTo - missingFrom + 1;

            // 缺口过大或持续过久 直接重同步
            if (gapCount > MaxNackRange || gapMs > 1000)
            {
                Logger.Warning("World frame gap too large: epoch={Epoch} from={From} to={To}",
                    Epoch, missingFrom, missingTo);
                RequestResync("gap too large");
                return;
            }

            // 等待超时 跳过缺口继续渲染
            if (gapMs > GapSkipMs || gapCount > GapSkipCount)
            {
                Logger.Warning("Skip world frame gap: epoch={Epoch} from={From} to={To}",
                    Epoch, missingFrom, missingTo);
                SkipGap();
                return;
            }

            // 先等待一小段时间 可靠传输下在途帧可能马上到达
            if (gapMs < NackFirstDelayMs) return;
            // 同时只保留一个补发请求
            if (nackTask != null && !nackTask.IsCompleted) return;
            // 控制补发重试频率
            if (nowMs - lastNackMs < NackRetryMs) return;

            lastNackMs = nowMs;
            NackSent++;
            // 单次补发请求限制最大范围
            var request = new WorldFrameRangeRequest
            {
                FromSequence = missingFrom,
                ToSequence = Math.Min(missingTo, missingFrom + MaxNackRange - 1),
            };
            try
            {
                nackTask = session.Server.RequestWorldFrames(request);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "RequestWorldFrames failed");
                nackTask = null;
            }
        }

        /// <summary>
        /// 取渲染时刻前后两个世界帧中的英雄状态
        /// 只有一侧存在时前后都用该状态
        /// </summary>
        public bool TryGetHeroBracket(
            string guid,
            double nowSec,
            out HeroDynamic previous,
            out HeroDynamic next,
            out double alpha,
            out double renderTimeSec)
        {
            previous = null!;
            next = null!;
            alpha = 0;
            // 渲染时间回退一个插值延迟
            renderTimeSec = nowSec - InterpolationDelaySeconds;

            if (!timeline.TryGetInterpolation(nowSec, out var prevFrame, out var nextFrame, out alpha))
                return false;

            // 同一英雄在前后两帧中的状态
            var prev = FindHero(prevFrame, guid);
            var nextState = FindHero(nextFrame, guid);

            // 两帧中都不存在该英雄
            if (prev == null && nextState == null)
                return false;

            // 关卡变化是瞬移,取较新的那一侧
            if (prev != null && nextState != null
                && (prev.LevelId != nextState.LevelId
                    || prev.SubLevelIndex != nextState.SubLevelIndex))
            {
                previous = nextState;
                next = nextState;
                alpha = 0;
                return true;
            }

            // 只有一侧存在时保持该状态
            previous = prev ?? nextState!;
            next = nextState ?? prev!;
            return true;
        }

        /// <summary>
        /// 尝试获取英雄静态数据
        /// </summary>
        public bool TryGetSpawn(string guid, out HeroSpawn spawn)
            => spawns.TryGetValue(guid, out spawn!);

        /// <summary>
        /// 请求全量世界基线
        /// </summary>
        public void RequestResync(string reason)
        {
            // 已有请求在途 不重复发送
            if (resyncTask != null && !resyncTask.IsCompleted) return;

            // 限制重同步频率
            long now = Environment.TickCount64;
            if (now - lastResyncRequestLocalMs < ResyncRetryMs) return;

            lastResyncRequestLocalMs = now;
            ResyncCount++;

            // 正常同步原因记 Information 缺口或溢出记 Warning
            if (reason is "initial" or "epoch changed" or "baseline retry" or "debug")
                Logger.Information("Request world resync: epoch={Epoch} reason={Reason}", Epoch, reason);
            else
                Logger.Warning("Request world resync: epoch={Epoch} reason={Reason}", Epoch, reason);

            try
            {
                resyncTask = session.Server.RequestWorldResync(Epoch);
            }
            catch (Exception ex)
            {
                // 失败时清空任务 下一 tick 重试
                Logger.Warning(ex, "RequestWorldResync failed");
                resyncTask = null;
            }
        }

        /// <summary>
        /// 重置管理器状态
        /// </summary>
        public void Reset()
        {
            timeline.Clear();
            pending.Clear();
            spawns.Clear();
            Epoch = -1;
            LastDeliveredSequence = -1;
            nextExpected = 0;
            hasBaseline = false;
            gapSinceMs = null;
            lastNackMs = 0;
            lastResyncRequestLocalMs = 0;
            nackTask = null;
            resyncTask = null;
            loggedFirstFrame = false;
        }

        /// <summary>
        /// 释放管理器
        /// </summary>
        public void Dispose()
        {
            Reset();
            FrameApplied = null;

#if DEBUG
            if (WorldFrameDebug.ClientManager == this)
                WorldFrameDebug.ClientManager = null;
#endif
        }

        /// <summary>
        /// 轮询重同步任务
        /// </summary>
        private void PollResync()
        {
            if (resyncTask == null || !resyncTask.IsCompleted) return;

            if (resyncTask.IsCompletedSuccessfully)
            {
                var resync = resyncTask.Result;
                resyncTask = null;
                OnResync(resync);
            }
            else
            {
                Logger.Warning(resyncTask.Exception, "RequestWorldResync faulted");
                resyncTask = null;
            }
        }

        /// <summary>
        /// 轮询补发任务
        /// </summary>
        private void PollNack()
        {
            if (nackTask == null || !nackTask.IsCompleted) return;

            if (nackTask.IsCompletedSuccessfully)
            {
                var bundle = nackTask.Result;
                nackTask = null;
                OnFrameBundle(bundle);
            }
            else
            {
                Logger.Warning(nackTask.Exception, "RequestWorldFrames faulted");
                nackTask = null;
            }
        }

        /// <summary>
        /// 缓存乱序帧
        /// </summary>
        private void BufferPending(WorldFrame frame)
        {
            // 已缓存过该序号
            if (pending.ContainsKey(frame.Sequence)) return;

            // 超过上限说明缺口长期无法补齐 清空并请求重同步
            if (pending.Count >= MaxPending)
            {
                PendingOverflow++;
                pending.Clear();
                gapSinceMs = null;
                RequestResync("pending overflow");
                return;
            }

            pending[frame.Sequence] = frame;
        }

        /// <summary>
        /// 应用连续帧
        /// </summary>
        private void ApplyFrame(WorldFrame frame)
        {
            if (frame == null) return;

            double timeSec = frame.ServerTimeMs / 1000.0;

            // 跳缺口后不要跨越长时间空白做插值
            var entries = timeline.Entries;
            if (entries.Count > 0 && timeSec - entries[^1].time > 0.5)
                timeline.Clear();

            // 按服务端时间写入时间线 只保留最近 2 秒
            timeline.AddSnapshot(timeSec, frame);
            timeline.Trim(timeSec - 2.0, MaxPending);

            // 缓存最新静态数据 供实体创建使用
            if (frame.Spawned != null)
            {
                foreach (var spawn in frame.Spawned)
                    if (spawn != null)
                        spawns[spawn.GUID] = spawn;
            }

            // 推进连续序号
            LastDeliveredSequence = frame.Sequence;
            nextExpected = frame.Sequence + 1;
            gapSinceMs = null;

            // 首帧日志用于确认世界帧链路正常
            if (!loggedFirstFrame)
            {
                loggedFirstFrame = true;
                Logger.Information(
                    "First world frame applied: epoch={Epoch} seq={Sequence} heroes={Heroes}",
                    frame.Epoch, frame.Sequence, frame.Heroes?.Count ?? 0);
            }

            // 校验静态数据后通知实体做存在性对账
            ValidateSpawns(frame);
            FrameApplied?.Invoke(frame);
        }

        /// <summary>
        /// 排空连续待处理帧
        /// </summary>
        private void DrainContiguous()
        {
            // 按序号连续应用缓存帧
            while (pending.Remove(nextExpected, out var frame))
                ApplyFrame(frame);
        }

        /// <summary>
        /// 跳过缺口
        /// </summary>
        private void SkipGap()
        {
            if (pending.Count == 0)
            {
                gapSinceMs = null;
                return;
            }

            // 跳到最早可用的乱序帧
            long min = pending.Keys.Min();
            long skipped = min - nextExpected;
            // 统计实际跳过的帧数
            if (skipped > 0)
                SkippedFrames += (int)skipped;

            nextExpected = min;
            DrainContiguous();
            gapSinceMs = null;
        }

        /// <summary>
        /// 校验英雄静态数据版本
        /// </summary>
        private void ValidateSpawns(WorldFrame frame)
        {
            if (frame.Heroes == null) return;

            foreach (var hero in frame.Heroes)
            {
                if (hero == null) continue;
                // 静态数据版本不一致时请求全量基线
                if (!spawns.TryGetValue(hero.GUID, out var spawn)
                    || spawn.Version != hero.SpawnVersion)
                {
                    RequestResync("spawn version");
                    return;
                }
            }
        }

        /// <summary>
        /// 在世界帧中查找英雄状态
        /// </summary>
        private static HeroDynamic? FindHero(WorldFrame frame, string guid)
        {
            if (frame?.Heroes == null) return null;
            // 英雄数量很少 线性查找即可
            foreach (var hero in frame.Heroes)
                if (hero != null && hero.GUID == guid)
                    return hero;
            return null;
        }
    }
}
