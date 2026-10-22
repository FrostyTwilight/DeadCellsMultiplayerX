using CoreLibrary.Core.Extensions;
using dc;
using dc.en;
using dc.hl.types;
using dc.libs.heaps.slib;
using dc.libs.heaps.slib._AnimManager;
using dc.pr;
using DeadCellsMultiplayerX.Client.Guest.WorldX.Snapshot;
using DeadCellsMultiplayerX.Common.Data;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Common.Serializers;
using DeadCellsMultiplayerX.Common.Tools;
using DeadCellsMultiplayerX.Utils;
using Hashlink.Virtuals;
using ModCore.Serialization;
using ModCore.Storage;
using ModCore.Utilities;
using Serilog;

namespace DeadCellsMultiplayerX.Client.Guest.WorldX.Remote
{
    internal class RemoteHero : KingSkin, IHxbitSerializable<RemoteHero.IHxData>
    {
        private readonly GuestClientSession session;
        private readonly WorldFrameManager frames;

        private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "RemoteHero");

        /// <summary>
        /// 当前的动画id
        /// </summary>
        private string lastGroup = "";

        /// <summary>
        /// 用于判断动画开始与结束时间
        /// </summary>
        private long lastAppliedStartTime = -1;

        /// <summary>
        /// 初始化后的首次更新后打印日志
        /// </summary>
        private bool loggedFirstTick;

        /// <summary>
        /// 用于切换关卡时首次打印日志
        /// </summary>
        private bool loggedLevelWait;

        /// <summary>
        /// 实体唯一id 由服务端提供
        /// </summary>
        public string Guid { get; }

        /// <summary>
        /// 当前的关卡id
        /// </summary>
        public string LevelId { get; set; } = string.Empty;

        /// <summary>
        /// 当前的关卡中进入的子关卡id
        /// </summary>
        public int SubLevelIndex { get; set; } = -1;


        /// <summary>
        /// 实体实际创建所在的关卡id,用于关卡变化时销毁重建
        /// </summary>
        public string CreatedLevelId { get; }

        /// <summary>
        /// 实体实际创建所在的子关卡id,用于关卡变化时销毁重建
        /// </summary>
        public int CreatedSubLevelIndex { get; }


        /// <summary>
        /// 该实体应当显示的目标关卡id(用于避免插值影响)
        /// </summary>
        public string TargetLevelId { get; private set; } = string.Empty;

        /// <summary>
        /// 该实体应当显示的目标子关卡id(用于避免插值影响)
        /// </summary>
        public int TargetSubLevelIndex { get; private set; } = -1;

        /// <summary>
        /// 序列化数据
        /// </summary>
        public class IHxData { }

        public RemoteHero(
            GuestClientSession session,
            Level lvl,
            WorldFrameManager frames,
            string guid,
            int x = 0,
            int y = 0) : base(lvl, x, y)
        {
            this.session = session;
            this.frames = frames;
            Guid = guid;
            CreatedLevelId = lvl?.map?.id?.ToString() ?? string.Empty;
            CreatedSubLevelIndex = lvl?.GetSubLevelIndex() ?? -1;

            set_easeSpritePos(false);
            hasWineGlass = false;
        }

        /// <summary>
        /// 父类初始化,禁用影响渲染字段
        /// </summary>
        public override void init()
        {
            base.init();
            hasRepelling = false;
            hasGravity = false;
            gravity = 0;
            collisionMode = new CollisionMode.None();
            frict = 0;
        }

        public override void initGfx()
        {
            base.initGfx();

            //移除状态机动画
            AnimManager anim = spr.get_anim();
            anim.removeAllStateAnims();

            //创建光
            createConfLight("Hero".AsHaxeString());
        }


        /// <summary>
        /// 用于首次初始化和皮肤变化多次初始化
        /// </summary>
        /// <param name="spawn">角色出生数据。</param>
        /// <remarks>
        /// 数据类型为 <see cref="HeroSpawn"/>。
        /// 参数 <paramref name="spawn"/> 不应为空
        /// </remarks>
        public void ApplySpawn(HeroSpawn spawn)
        {
            // 校验初始化所需的数据是否完整
            if (string.IsNullOrEmpty(spawn.ColorMapModel)
                || string.IsNullOrEmpty(spawn.ColorMapSkin)
                || spawn.MainSprite == null) return;

            //初始化贴图
            SpriteLib sprlib = Assets.Class.lib.get(spawn.MainSprite.AtlasName.AsHaxeString());
            dc.String group = spawn.MainSprite.GroupName.AsHaxeString();
            dc.h3d.mat.Texture normalMapFromGroup = sprlib.getNormalMapFromGroup(group);
            initSprite(sprlib, group, null, null, null, true, null, normalMapFromGroup);

            //设置枢轴
            spr.pivot.copyFrom(
                DCMXSerializers.MessagePack.Deserialize<SpritePivot>(spawn.MainSprite.PivotData));

            //上色
            setColorMap(
                spawn.ColorMapModel.AsHaxeString(),
                spawn.ColorMapSkin.AsHaxeString(),
                null);

            //设置skin发光色
            foreach (var (idx, gdd) in spawn.GlowData)
            {
                if (gdd == null) continue;
                setGlowData(idx,
                    DCMXSerializers.MessagePack.Deserialize<virtual_animationIntensity_animationScale_animationSpeed_animationTextureMask_inner_key_outer_power_>(gdd),
                    spr);
            }
        }

        public override void fixedUpdate()
        {
            double nowSec = session.SyncedTimeMs / 1000.0;

            if (frames.TryGetHeroBracket(
                    Guid, nowSec,
                    out HeroDynamic prev, out HeroDynamic next,
                    out double alpha, out double renderTimeSec))
            {
                // 时间线还没追上目标关卡时,保持创建时的摆放,不使用旧关卡样本
                if (prev.LevelId != TargetLevelId || prev.SubLevelIndex != TargetSubLevelIndex)
                {
                    if (!loggedLevelWait)
                    {
                        loggedLevelWait = true;
                        Log.Information(
                            "RemoteHero {Guid} waiting for level {TargetLevelId}#{TargetSub}: sample={SampleLevel}#{SampleSub}",
                            Guid, TargetLevelId, TargetSubLevelIndex,
                            prev.LevelId, prev.SubLevelIndex);
                    }

                    base.fixedUpdate();
                    return;
                }

                if (!loggedFirstTick)
                {
                    loggedFirstTick = true;
                    Log.Information(
                        "RemoteHero {Guid} first tick: prev={PrevLevel}#{PrevSub} next={NextLevel}#{NextSub} " +
                        "prevPos=({PrevX},{PrevY}) nextPos=({NextX},{NextY}) alpha={Alpha}",
                        Guid,
                        prev.LevelId, prev.SubLevelIndex,
                        next.LevelId, next.SubLevelIndex,
                        prev.Pos.CX + prev.Pos.XR, prev.Pos.CY + prev.Pos.XY,
                        next.Pos.CX + next.Pos.XR, next.Pos.CY + next.Pos.XY,
                        alpha);
                }

                //此方法控制实体平滑移动
                RemoteHeroMotion.Apply(this, prev.Pos, next.Pos, alpha);

                //同步状态机数组
                AffectCodec.Apply(this, prev.Affects, renderTimeSec);

                Animate(prev.Anim, next.Anim, renderTimeSec);
            }

            base.fixedUpdate();
        }

        /// <summary>
        /// 创建后直接摆放到触发创建的状态位置,避免落到旧关卡坐标
        /// </summary>
        /// <param name="levelId">当前的关卡id</param>
        /// <param name="subLevelIndex">当前的子关卡id</param>
        /// <param name="pos">位置信息</param>
        public void SnapToState(string levelId, int subLevelIndex, PosVector pos)
        {
            TargetLevelId = levelId;
            TargetSubLevelIndex = subLevelIndex;

            if (pos != null)
                RemoteHeroMotion.SnapTo(this, pos);
        }

        /// <summary>
        /// 更新实体动画
        /// </summary>
        /// <param name="prevAnim">上一个动画快照</param>
        /// <param name="nextAnim">下一个动画快照</param>
        /// <param name="renderTimeSec">客户端时间,用于判断动画开始与结束时间</param>
        public void Animate(AnimInfo prevAnim, AnimInfo nextAnim, double renderTimeSec)
        {
            AnimManager? anim = spr?.get_anim();
            if (anim == null) return;
            if (prevAnim == null && nextAnim == null) return;

            AnimInfo chosen = nextAnim;
            if (nextAnim == null)
            {
                chosen = prevAnim!;
            }
            else if (prevAnim != null)
            {
                double nextStartSec = nextAnim.StartTime / 1000.0;
                if (renderTimeSec < nextStartSec)
                    chosen = prevAnim;
            }

            if (chosen == null) return;

            string groupName = chosen.GroupName;
            if (string.IsNullOrEmpty(groupName)) return;

            // 打断翻滚
            if (IsRollRelatedGroup(groupName) && IsRollAct())
            {
                lastGroup = "";
                lastAppliedStartTime = -1;
                return;
            }

            bool needPlay = lastAppliedStartTime != chosen.StartTime
                         || lastGroup != groupName;

            if (needPlay)
            {
                lastGroup = groupName;
                lastAppliedStartTime = chosen.StartTime;

                AnimInstance? cur = anim.stack?.getDyn(0) as AnimInstance;
                if (cur != null) cur.plays = 0;

                anim.play(groupName.AsHaxeString(), chosen.Plays, null);
            }

            AnimInstance? stack = anim.stack?.getDyn(0) as AnimInstance;
            if (stack != null)
            {
                stack.speed = chosen.Speed;
                stack.paused = chosen.Paused;
                stack.playDuration = chosen.playDuration;
            }
        }

        /// <summary>
        /// 用于判断服务端发送的实体状态中,是否存在某个id
        /// </summary>
        /// <param name="affectId">状态对应索引id</param>
        /// <returns>bool(数组是否为空)</returns>
        private bool IsAffectActive(int affectId)
        {
            var affects = this.affects;
            if (affects == null) return false;
            if (affectId < 0 || affectId >= affects.length) return false;
            if (!(affects.array[affectId] is ArrayObj list)) return false;
            return list.length > 0;
        }

        /// <summary>
        /// 判断当前实体是否在做翻滚动画
        /// </summary>
        /// <param name="name"></param>
        /// <remarks>由于原始翻滚技能使用状态打断,这里必须判断,否则会出现动画快速切换(闪烁)</remarks>
        /// <returns>bool</returns>
        private static bool IsRollRelatedGroup(string name)
        {
            switch (name)
            {
                case "rolling":
                case "rollEnd":
                case "rollIdle":
                case "rollRun":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 强制开启渲染
        /// </summary>
        /// <returns></returns>
        public override bool _isOnScreen() => true;

        /// <summary>
        /// 判断此玩家控制的hero是否处于翻滚状态
        /// </summary>
        /// <returns></returns>
        private bool IsRollAct() => IsAffectActive(3);

        IHxData IHxbitSerializable<IHxData>.GetData()
        {
            return new();
        }

        void IHxbitSerializable<IHxData>.SetData(IHxData data)
        {
        }
    }
}
