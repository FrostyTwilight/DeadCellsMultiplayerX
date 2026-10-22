using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Client.Event;
using DeadCellsMultiplayerX.Common;
using DeadCellsMultiplayerX.Utils;

namespace DeadCellsMultiplayerX.Client.Guest.WorldX.LocalHero
{
    /// <summary>
    /// 本地 Hero 状态管理器
    /// 追踪本地英雄的状态
    /// </summary>
    internal class LocalHeroState : DisposableEventReceiver,
        IOnLocalHeroInitDone,
        IOnLocalHeroPostUpdate
    {
        private readonly GuestClientSession session;
        private readonly RemoteWorld remoteWorld;

        /// <summary>
        /// 本地hero基本信息
        /// </summary>
        private readonly LocalHeroContext ctx = new();

#if DEBUG


        /// <summary>
        /// 用于调试关卡改变时打印日志
        /// </summary>
        /// <remarks>
        /// 存储格式为：<c>string lastLevelSignature = $"{LevelId}#{SubLevelIndex}";</c>
        /// </remarks>
        private string DEBUGlastLevelSignature = string.Empty;
#endif

        /// <summary>
        /// 外部获取hero上下文
        /// </summary>
        public LocalHeroContext Context => ctx;

        /// <summary>
        /// 同步时间
        /// </summary>
        public double SyncedTimeMs => session.SyncedTimeMs;

        public LocalHeroState(GuestClientSession session, RemoteWorld remoteWorld)
        {
            this.session = session;
            this.remoteWorld = remoteWorld;

            ctx.HasHero = true;
            ctx.IsAlive = !session.Game.hero.destroyed;

            SyncLevelSnapshot(session.Game);
        }

        void IOnLocalHeroInitDone.OnHeroInitDone(Hero hero)
        {
            var game = Game.Class.ME;
            if (hero != game?.hero) return;

            ctx.HasHero = true;
            ctx.IsAlive = !hero.destroyed;

            SyncLevelSnapshot(game);
        }

        void IOnLocalHeroPostUpdate.OnPostUpdate(Hero hero)
        {
            var game = Game.Class.ME;
            if (hero != game?.hero) return;

            ctx.HasHero = !hero.destroyed;
            ctx.IsAlive = ctx.HasHero && hero.life > 0;
            ctx.InCinematic = game.hasCinematic();

            UpdateLevel(game, hero);
        }

        private void UpdateLevel(Game game, Hero hero)
        {
            if (game == null)
            {
                ResetLevelInfo();
                return;
            }

            // hero存在时才进行状态刷新
            var level = hero?._level;
            if (level == null)
            {
                return;
            }

            ctx.CurrentLevelId = level.map?.id?.ToString() ?? string.Empty;
            ctx.SubLevelIndex = SafeGetSubLevelIndex(level);
            ctx.InSubLevel = IsInSubLevel(game, level);

#if DEBUG
            string signature = $"{ctx.CurrentLevelId}#{ctx.SubLevelIndex}";
            if (signature != DEBUGlastLevelSignature)
            {
                DEBUGlastLevelSignature = signature;
                Logger.Information(
                    "Local level: {LevelId}#{SubLevelIndex} inSubLevel={InSubLevel}",
                    ctx.CurrentLevelId, ctx.SubLevelIndex, ctx.InSubLevel);
            }
#endif
        }

        /// <summary>
        /// 用于hero初始化时更新关卡信息
        /// </summary>
        /// <param name="game"></param>
        private void SyncLevelSnapshot(Game game)
        {
            if (game == null) return;

            var level = game.hero?._level ?? game.curLevel;
            if (level == null) return;

            ctx.CurrentLevelId = level.map?.id?.ToString() ?? string.Empty;
            ctx.SubLevelIndex = SafeGetSubLevelIndex(level);
            ctx.InSubLevel = IsInSubLevel(game, level);
        }

        /// <summary>
        /// 子关卡id判断
        /// </summary>
        /// <param name="game"></param>
        /// <param name="curLevel"></param>
        /// <returns></returns>
        private static bool IsInSubLevel(Game game, Level curLevel)
        {
            var subLevels = game.subLevels;
            if (subLevels == null || subLevels.length == 0)
                return false;

            var mainLevel = subLevels.getDyn(0);
            return mainLevel != null && curLevel != mainLevel;
        }

        
        private static int SafeGetSubLevelIndex(Level level)
        {
            try { return level.GetSubLevelIndex(); }
            catch { return -1; }
        }

        /// <summary>
        /// 重置关卡信息
        /// </summary>
        private void ResetLevelInfo()
        {
            ctx.CurrentLevelId = string.Empty;
            ctx.SubLevelIndex = -1;
            ctx.InSubLevel = false;
        }

        //查询
        public bool HasHero() => ctx.HasHero;
        public bool IsAlive() => ctx.IsAlive;
        public bool IsInSubLevel() => ctx.InSubLevel;
        public bool IsInCinematic() => ctx.InCinematic;

        public bool ShouldBroadcast()
            => ctx.HasHero && ctx.IsAlive && !ctx.InSubLevel;

        protected override void MyDispose()
        {
            base.MyDispose();
        }
    }
}