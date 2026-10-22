using System.Collections.Generic;
using MessagePack;

namespace DeadCellsMultiplayerX.Common.Data.Snapshot
{
    /// <summary>
    /// 服务端每 tick 广播的完整世界帧
    /// 怪物在此扩展
    /// </summary>
    [MessagePackObject]
    public class WorldFrame
    {
        /// <summary>
        /// 世界重置代号
        /// </summary>
        [Key(0)] public int Epoch;

        /// <summary>
        /// 广播序号,epoch 内从 0 连续
        /// </summary>
        [Key(1)] public long Sequence;

        /// <summary>
        /// 服务端时间戳(毫秒)
        /// </summary>
        [Key(2)] public double ServerTimeMs;

        /// <summary>
        /// 全量英雄动态状态
        /// </summary>
        [Key(3)] public List<HeroDynamic> Heroes = [];

        /// <summary>
        /// 新增/静态变化的英雄
        /// </summary>
        [Key(4)] public List<HeroSpawn> Spawned = [];

        /// <summary>
        /// 本帧移除的 GUID
        /// </summary>
        [Key(5)] public List<string> Despawned = [];
    }
}
