using System.Collections.Generic;
using MessagePack;

namespace DeadCellsMultiplayerX.Common.Data.Snapshot
{
    /// <summary>
    /// 客户端每帧上传的本地世界帧
    /// 后续本地怪物/交互物在此扩展
    /// </summary>
    [MessagePackObject]
    public class ClientWorldFrame
    {
        /// <summary>
        /// 回显最近观测到的服务端 epoch
        /// </summary>
        [Key(0)] public int Epoch;

        /// <summary>
        /// 每个客户端独立,发送时分配
        /// </summary>
        [Key(1)] public long Sequence;

        /// <summary>
        /// SyncedTimeMs
        /// </summary>
        [Key(2)] public double ClientTimeMs;

        /// <summary>
        /// 帧id
        /// </summary>
        [Key(3)] public long FrameId;

        /// <summary>
        /// 收到的其他客户端实体
        /// </summary>
        [Key(4)] public List<HeroUpload> Heroes = [];
    }
}
