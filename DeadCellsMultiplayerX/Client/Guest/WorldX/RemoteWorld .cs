using System.Collections.Generic;
using dc.en;
using DeadCellsMultiplayerX.Client.Guest.WorldX.Remote;
using DeadCellsMultiplayerX.Client.Guest.WorldX.LocalHero;
using DeadCellsMultiplayerX.Client.Guest.WorldX.Snapshot;
using DeadCellsMultiplayerX.Common;
using DeadCellsMultiplayerX.Common.Data.Snapshot;

namespace DeadCellsMultiplayerX.Client.Guest.WorldX
{
    /// <summary>
    /// 客户端世界层
    /// </summary>
    internal class RemoteWorld : DisposableEventReceiver
    {
        /// <summary>
        /// 世界帧管理器，负责接收、应用和驱动世界帧
        /// </summary>
        private readonly WorldFrameManager frames;

        /// <summary>
        /// 远程英雄管理器，负责维护其他玩家的英雄状态
        /// </summary>
        private readonly RemoteHeroManager heroes;

        /// <summary>
        /// 本地英雄状态
        /// </summary>
        private readonly LocalHeroState localHero;

        /// <summary>
        /// 本地玩家的唯一标识
        /// </summary>
        private readonly string localGuid;

        public RemoteWorld(GuestClientSession session, string localGuid)
        {
            this.localGuid = localGuid;
            frames = new WorldFrameManager(session, localGuid);
            heroes = new RemoteHeroManager(session, this);
            localHero = new LocalHeroState(session, this);

            frames.FrameApplied += OnFrameApplied;
            frames.Start();
        }

        public LocalHeroState LocalHero => localHero;
        public RemoteHeroManager Heros => heroes;
        public WorldFrameManager Frames => frames;
        public string LocalGuid => localGuid;
        public IEnumerable<RemoteHero> AllHeroes => heroes.All;


        public bool TryGetHero(string guid, out RemoteHero hero)
            => heroes.TryGet(guid, out hero);

        /// <summary>
        /// 处理接收到的世界帧
        /// </summary>
        /// <param name="frame">要处理的世界帧。</param>
        public void OnWorldFrame(WorldFrame frame) => frames.OnFrame(frame);

        /// <summary>
        /// 推进世界帧管理器的定时逻辑
        /// </summary>
        /// <param name="nowMs">当前时间戳，单位为毫秒。</param>
        public void Tick(long nowMs) => frames.Tick(nowMs);

        /// <summary>
        /// 在世界帧应用完成后，将帧数据应用到远程英雄管理器
        /// </summary>
        /// <param name="frame">已应用的世界帧</param>
        private void OnFrameApplied(WorldFrame frame) => heroes.ApplyFrame(frame, frames);

        protected override void MyDispose()
        {
            frames.FrameApplied -= OnFrameApplied;
            heroes.Dispose();
            frames.Dispose();
            localHero.Dispose();
            base.MyDispose();
        }
    }
}
