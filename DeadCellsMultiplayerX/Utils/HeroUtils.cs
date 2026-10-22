using System;
using System.Runtime.CompilerServices;
using CoreLibrary.Core.Extensions;
using dc;
using dc.en;
using dc.en.hero;
using dc.libs.heaps.slib;
using dc.libs.heaps.slib._AnimManager;
using DeadCellsMultiplayerX.Common.Data;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Common.Serializers;
using Hashlink.Proxy;
using Hashlink.Virtuals;
using HaxeProxy.Runtime;
using ModCore.Utilities;

namespace DeadCellsMultiplayerX.Utils
{
    /// <summary>采集 Hero 同步信息</summary>
    public static class HeroUtils
    {
        private static readonly ConditionalWeakTable<AnimManager, AnimTracker> animStartCache = new();

        /// <summary>
        /// 采集一个 Hero 的同步信息
        /// </summary>
        public static (HeroSpawn spawn, HeroDynamic dyn) Collect(
            Entity hero,
            string guid,
            long remoteTime)
        {
            var spawn = new HeroSpawn { GUID = guid };
            var dyn = new HeroDynamic { GUID = guid, ChangeMask = HeroDynamic.BitAll };

            FillAll(hero, spawn, dyn, remoteTime);
            return (spawn, dyn);
        }

        public static void FillAll(
            Entity e,
            HeroSpawn spawn,
            HeroDynamic dyn,
            long remoteTime)
        {
            //spawn.TypeName = e.GetType().FullName ?? string.Empty;

            if (!e.initDone) return;

            spawn.SubLevelId = e._level.GetSubLevelIndex();
            dyn.Affects = AffectCodec.Collect(
                (Hero)e,
                remoteTime / 1000.0,
                e.cd.baseFps);

            spawn.LevelId = e._level.map?.id?.ToString() ?? string.Empty;
            dyn.LevelId = spawn.LevelId;
            dyn.SubLevelIndex = e._level.GetSubLevelIndex();
            dyn.ChangeMask |= HeroDynamic.BitLevel;

            if (e.spr != null)
            {
                dyn.Pos = new PosVector(
                    e.cx, e.cy, e.xr, e.yr, e.dir,
                    e.dx, e.dy, e.bdx, e.bdy);

                FillSkinInfo((Hero)e, spawn);
                FillAnimInfo(dyn, e.spr, remoteTime);
                FillGlowData(e, spawn);
            }
        }

        public static void FillSkinInfo(Hero hero, HeroSpawn spawn)
        {
            var skin = hero.getSkinInfo();
            spawn.ColorMapModel = skin.model.ToString();
            spawn.ColorMapSkin = skin.colorMap.ToString();

            spawn.MainSprite ??= new SpriteInfo();
            spawn.MainSprite.AtlasName = "atlas/" + spawn.ColorMapModel + ".atlas";
            spawn.MainSprite.GroupName = hero.spr.groupName.ToString();
            spawn.MainSprite.PivotData = DCMXSerializers.MessagePack.Serialize(hero.spr?.pivot);
            spawn.MainSprite.Parent = spawn.GUID;
        }

        public static void FillGlowData(Entity e, HeroSpawn spawn)
        {
            if (spawn.GlowData.Count > 0) return;

            var glow = (dc.shader.GlowKey)e.spr.getShader(dc.shader.GlowKey.Class);
            if (glow == null) return;

            var array = glow.getGlowDatas();
            for (int i = 0; i < array.length; i++)
            {
                var data = array.getDyn(i);
                var virtuals = ((HaxeProxyBase)data)
                    .ToVirtual<virtual_animationIntensity_animationScale_animationSpeed_animationTextureMask_inner_key_outer_power_>();
                spawn.GlowData.Add(i, DCMXSerializers.MessagePack.Serialize(virtuals));
            }
        }

        public static void FillAnimInfo(HeroDynamic dyn, HSprite spr, long remoteTime)
        {
            var anim = spr.get_anim();
            if (anim == null || anim.destroyed || anim.stack == null || anim.stack.length == 0)
                return;

            var current = anim.stack.getDyn(0) as AnimInstance;
            if (current == null) return;

            string groupName = spr.groupName?.ToString() ?? string.Empty;

            if (!animStartCache.TryGetValue(anim, out var tracker))
            {
                tracker = new AnimTracker { LastGroup = "", StartTime = remoteTime };
                animStartCache.Add(anim, tracker);
            }

            if (tracker.LastGroup != groupName)
            {
                tracker.LastGroup = groupName;
                tracker.StartTime = remoteTime;
            }

            dyn.Anim = new AnimInfo
            {
                Speed = current.speed,
                Paused = current.paused,
                Frame = spr.frame,
                Plays = current.plays,
                playDuration = current.playDuration,
                GroupName = groupName,
                StartTime = tracker.StartTime,
            };

            var transitions = anim.transitions;
            if (transitions != null
                && dyn.Anim.AnimTransitions.Count == 0
                && transitions.length > 0)
            {
                foreach (Transition data in transitions)
                {
                    dyn.Anim.AnimTransitions.Add(new AnimTransitions
                    {
                        Anim = data.anim.ToString(),
                        From = data.from.ToString(),
                        To = data.to.ToString(),
                        reverse = data.reverse,
                        speed = data.spd,
                    });
                }
            }
        }
    }
}