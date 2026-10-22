using System.Collections.Generic;
using dc.en;
using DeadCellsMultiplayerX.Client.Event;
using DeadCellsMultiplayerX.Client.Guest.WorldX.Remote;
using DeadCellsMultiplayerX.Client.Guest.WorldX.Snapshot;
using DeadCellsMultiplayerX.Common;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Utils;

namespace DeadCellsMultiplayerX.Client.Guest.WorldX
{
    /// <summary>
    /// 管理所有远端玩家
    /// 以服务端世界帧中的英雄集合为权威,负责创建、销毁与关卡过滤
    /// </summary>
    internal class RemoteHeroManager : DisposableEventReceiver,
    IOnLocalHeroInitDone
    {
        private readonly GuestClientSession session;
        private readonly RemoteWorld remoteWorld;

        /// <summary>
        /// 存储其他客户端玩家的实体
        /// </summary>
        private readonly Dictionary<string, RemoteHero> heroes = [];

#if DEBUG
        /// <summary>
        /// 用于调试其他客户端实体的生成
        /// </summary>
        private readonly Dictionary<string, string> mismatchLogged = [];

        /// <summary>
        /// 用于调试其他客户端实体的生成信息
        /// </summary>
        private string lastHeroSetSignature = string.Empty;
#endif
        /// <summary>
        /// 记录生成有误的玩家
        /// </summary>
        private readonly HashSet<string> missingSpawnLogged = [];
        public IEnumerable<RemoteHero> All => heroes.Values;

        /// <summary>
        /// 当前客户端位于的关卡id
        /// </summary>
        private string localLevelId => remoteWorld.LocalHero.Context.CurrentLevelId;

        /// <summary>
        /// 当前客户端位于的子关卡索引
        /// </summary>
        private int localSubLevel => remoteWorld.LocalHero.Context.SubLevelIndex;

        /// <summary>
        /// 客户id
        /// </summary>
        private string localGuid => remoteWorld.LocalGuid;

        public RemoteHeroManager(GuestClientSession session, RemoteWorld remoteWorld)
        {
            this.session = session;
            this.remoteWorld = remoteWorld;
        }

        /// <summary>
        /// 每应用一帧世界快照后做一次存在性对账
        /// </summary>
        /// <param name="frame">帧快照</param>
        /// <param name="frames">帧管理器</param>
        public void ApplyFrame(WorldFrame frame, WorldFrameManager frames)
        {
            if (frame == null) return;

#if DEBUG
            LogHeroSet(frame);
#endif

            // 静态外观变化时调用
            if (frame.Spawned != null)
            {
                foreach (var spawn in frame.Spawned)
                {
                    if (spawn == null || spawn.GUID == localGuid) continue;
                    if (heroes.TryGetValue(spawn.GUID, out var existing))
                        existing.ApplySpawn(spawn);
                }
            }

            var seen = new HashSet<string>();
            if (frame.Heroes != null)
            {
                foreach (var state in frame.Heroes)
                {
                    if (state == null || state.GUID == localGuid) continue;

                    //判断其他客户端是否与玩家在同一关卡,不在即销毁其他客户端实体
                    if (!IsLocalLevel(state))
                    {
#if DEBUG
                        LogMismatch(state);
#endif
                        Despawn(state.GUID);
                        continue;
                    }

#if DEBUG
                    mismatchLogged.Remove(state.GUID);
#endif
                    seen.Add(state.GUID);

                    heroes.TryGetValue(state.GUID, out var hero);


                    if (hero == null)
                    {
                        if (!frames.TryGetSpawn(state.GUID, out var spawn))
                        {
                            if (missingSpawnLogged.Add(state.GUID))
                            {
                                Logger.Warning(
                                    "No spawn for remote hero {Guid} on level {LevelId}#{SubLevelIndex}",
                                    state.GUID, state.LevelId, state.SubLevelIndex);
                            }
                            continue;
                        }

                        hero = CreateHero(spawn, frames, state);
                        missingSpawnLogged.Remove(state.GUID);
                    }

                    hero.LevelId = state.LevelId;
                    hero.SubLevelIndex = state.SubLevelIndex;
                }
            }

            // 服务端显式移除
            if (frame.Despawned != null)
            {
                foreach (var guid in frame.Despawned)
                {
                    if (string.IsNullOrEmpty(guid) || guid == localGuid) continue;
                    Despawn(guid);
                }
            }

            // 不再出现在权威集合中的实体
            var toRemove = new List<string>();
            foreach (var guid in heroes.Keys)
                if (!seen.Contains(guid))
                    toRemove.Add(guid);
            foreach (var guid in toRemove)
                Despawn(guid);
        }

        /// <summary>
        /// 销毁其中一位客户端实体
        /// </summary>
        /// <param name="guid">客户id</param>
        public void Despawn(string guid)
        {
            if (heroes.Remove(guid, out var hero))
            {
                Logger.Information("Despawn remote hero {Guid}", guid);
                hero?.destroy();
            }
        }

        public bool TryGet(string guid, out RemoteHero hero)
            => heroes.TryGetValue(guid, out hero!);

        protected override void MyDispose()
        {
            foreach (var h in heroes.Values)
                h?.destroy();
            heroes.Clear();
        }

        /// <summary>
        /// 遍历所有 hero，销毁 level 不匹配的客户端实体
        /// </summary>
        public void RefreshLevel()
        {
            var toRemove = new List<string>();
            foreach (var (guid, hero) in heroes)
            {
                if (!IsLocalLevel(hero))
                    toRemove.Add(guid);
            }
            foreach (var guid in toRemove)
                Despawn(guid);
        }

        /// <summary>
        /// 创建客户实体
        /// </summary>
        /// <param name="spawn">客户玩家显示数据</param>
        /// <param name="frames">帧管理器</param>
        /// <param name="state">客户实体状态数据</param>
        /// <returns><see cref="RemoteHero"/></returns>
        private RemoteHero CreateHero(HeroSpawn spawn, WorldFrameManager frames, HeroDynamic state)
        {
            var game = session.Game;
            var level = game.curLevel;
            var hero = new RemoteHero(session, level, frames, spawn.GUID);
            hero.init();
            hero.ApplySpawn(spawn);
            heroes[spawn.GUID] = hero;

            // 直接用触发创建的新关卡状态摆放,避免用旧关卡插值坐标
            hero.SnapToState(state.LevelId, state.SubLevelIndex, state.Pos);

            var heroLevel = game.hero?._level;
            Logger.Information(
                "Created remote hero {Guid}: used={UsedLevel}#{UsedSub} state={StateLevel}#{StateSub} " +
                "cur={CurLevel}#{CurSub} heroLevel={HeroLevel}#{HeroSub} " +
                "statePos=({StateX},{StateY}) pos=({X},{Y}) destroyed={Destroyed}",
                spawn.GUID,
                LevelId(level), LevelIndex(level),
                state.LevelId, state.SubLevelIndex,
                LevelId(game.curLevel), LevelIndex(game.curLevel),
                LevelId(heroLevel), LevelIndex(heroLevel),
                state.Pos?.CX + state.Pos?.XR, state.Pos?.CY + state.Pos?.XY,
                hero.cx + hero.xr, hero.cy + hero.yr,
                hero.destroyed);

            return hero;
        }

        private static string LevelId(dc.pr.Level? level)
            => level?.map?.id?.ToString() ?? "<null>";

        private static int LevelIndex(dc.pr.Level? level)
            => level?.GetSubLevelIndex() ?? -1;


#if DEBUG
        private void LogMismatch(HeroDynamic state)
        {
            string key = $"{state.LevelId}#{state.SubLevelIndex}|{localLevelId}#{localSubLevel}";
            if (mismatchLogged.TryGetValue(state.GUID, out var last) && last == key)
                return;

            mismatchLogged[state.GUID] = key;
            Logger.Information(
                "Remote hero level mismatch: guid={Guid} state={StateLevel}#{StateSub} local={LocalLevel}#{LocalSub}",
                state.GUID, state.LevelId, state.SubLevelIndex, localLevelId, localSubLevel);
        }


        private void LogHeroSet(WorldFrame frame)
        {
            if (frame.Heroes == null) return;

            var sb = new System.Text.StringBuilder();
            foreach (var hero in frame.Heroes)
            {
                if (hero == null) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(hero.GUID).Append('@')
                  .Append(hero.LevelId).Append('#')
                  .Append(hero.SubLevelIndex);
            }

            string signature = sb.ToString();
            if (signature == lastHeroSetSignature) return;

            lastHeroSetSignature = signature;
            Logger.Information("World frame heroes: {Heroes}", signature);
        }
#endif

        private bool IsLocalLevel(HeroDynamic state)
            => state.LevelId == localLevelId && state.SubLevelIndex == localSubLevel;

        private bool IsLocalLevel(RemoteHero hero)
            => hero.CreatedLevelId == localLevelId
            && hero.CreatedSubLevelIndex == localSubLevel;

        void IOnLocalHeroInitDone.OnHeroInitDone(Hero hero)
        {
            RefreshLevel();
        }
    }
}
