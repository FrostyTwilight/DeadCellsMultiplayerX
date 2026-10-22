using System.Collections.Generic;
using MessagePack;

namespace DeadCellsMultiplayerX.Common.Data.Snapshot
{
    /// <summary>
    /// 客户端请求服务端补发广播帧
    /// </summary>
    [MessagePackObject]
    public class WorldFrameRangeRequest
    {
        [Key(0)] public long FromSequence;
        [Key(1)] public long ToSequence;
    }

    /// <summary>
    /// 服务端请求客户端补发上传帧
    /// </summary>
    [MessagePackObject]
    public class ClientFrameRangeRequest
    {
        [Key(0)] public long FromSequence;
        [Key(1)] public long ToSequence;
    }

    /// <summary>
    /// 服务端对补发请求的响应
    /// </summary>
    [MessagePackObject]
    public class WorldFrameBundle
    {
        [Key(0)] public List<WorldFrame> Frames = [];
        [Key(1)] public WorldResync? Resync;
    }

    /// <summary>
    /// 客户端对补发请求的响应
    /// </summary>
    [MessagePackObject]
    public class ClientFrameBundle
    {
        [Key(0)] public List<ClientWorldFrame> Frames = [];
        [Key(1)] public bool Unavailable;
    }

    /// <summary>
    /// 全量世界基线,用于历史帧已被淘汰或首次同步
    /// </summary>
    [MessagePackObject]
    public class WorldResync
    {
        [Key(0)] public int Epoch;
        
        /// <summary>
        /// 基线序号,客户端 next = Sequence + 1
        /// </summary>
        [Key(1)] public long Sequence;
        [Key(2)] public double ServerTimeMs;
        [Key(3)] public List<HeroDynamic> Heroes = [];
        [Key(4)] public List<HeroSpawn> Spawns = [];
    }
}
