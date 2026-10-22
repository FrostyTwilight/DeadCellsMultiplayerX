using dc;
using dc.en;
using dc.en.hero;
using dc.libs.heaps.slib;
using dc.libs.heaps.slib._AnimManager;
using dc.tool.mainSkills;
using DeadCellsMultiplayerX.Common.Data;
using DeadCellsMultiplayerX.Common.Serializers;
using DeadCellsMultiplayerX.Server.Events;
using DeadCellsMultiplayerX.Utils;
using Hashlink.Virtuals;
using HaxeProxy.Runtime;
using MessagePack.Formatters;
using ModCore.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DeadCellsMultiplayerX.Server.Connection
{
    internal partial class SGuestConnection
    {
        private readonly Dictionary<nint, SpriteInfo> spritesInfo = [];

        private SpriteInfo GetSpriteInfo(HSprite spr)
        {
            if (!spritesInfo.TryGetValue(spr.HashlinkPointer, out var result))
            {
                result = new();
                spritesInfo.Add(spr.HashlinkPointer, result);
            }
            return result;
        }

        private void FillSpriteInfo(HSprite spr, string? parent, SpriteInfo inf)
        {
            if (ServerMain.Instance.spriteLib2altas.TryGetValue(spr.lib, out var atlasPath))
            {
                inf.AtlasName = atlasPath;
                inf.GroupName = spr.groupName.ToString();
            }

            inf.PivotData = DCMXSerializers.MessagePack.Serialize(spr?.pivot);
            inf.Parent = parent;

            var children = spr?.children;

            inf.Children.Clear();
            for (int i = 0; i < children?.length; i++)
            {
                var child = children.getDyn(i) as HSprite;
                if (child == null)
                {
                    continue;
                }

                var sinfo = GetSpriteInfo(child);
                inf.Children.Add(sinfo);
                FillSpriteInfo(child, inf.GUID, sinfo);
            }
        }

        public void FillEntityAnimInfo(EntityInfo inf, HSprite spr)
        {
            var anim = spr.get_anim();
            if (spr != null && anim != null && !anim.destroyed && anim.stack.length > 0)
            {
                var current = anim.stack.getDyn(0) as AnimInstance;
                var transitions = anim.transitions;
                if (current != null)
                {
                    AnimInfo info = new AnimInfo
                    {
                        Speed = current.speed,
                        Paused = current.paused,
                        Frame = spr.frame,
                        Plays = current.plays,
                        playDuration = current.playDuration,
                    };

                    if (transitions != null && inf.animInfo.AnimTransitions.Count == 0 && transitions.length > 0)
                    {
                        foreach (Transition data in transitions)
                        {
                            var tr = new AnimTransitions();
                            tr.Anim = data.anim.ToString();
                            tr.From = data.from.ToString();
                            tr.To = data.to.ToString();
                            tr.reverse = data.reverse;
                            tr.speed = data.spd;

                            inf.animInfo.AnimTransitions.Add(tr);
                        }
                    }
                    inf.animInfo = info;
                }
            }
        }

        public void FillEntityGlowkeyData(Entity e, EntityInfo info)
        {
            var glow = (dc.shader.GlowKey)e.spr.getShader(dc.shader.GlowKey.Class);
            if (info.GlowData.Count == 0 && glow != null)
            {
                var array = glow.getGlowDatas();
                for (int i = 0; i < array.length; i++)
                {
                    var data = array.getDyn(i);
                    var virtuals = ((HaxeProxyBase)data).ToVirtual<virtual_animationIntensity_animationScale_animationSpeed_animationTextureMask_inner_key_outer_power_>();
                    info.GlowData.Add(i, DCMXSerializers.MessagePack.Serialize(virtuals));
                }
            }
        }

        private void FillEntityInfo(Entity e, EntityInfo inf)
        {
            inf.TypeName = e.GetType().FullName;

            if (!e.initDone) return;

            inf.SubLevelId = e._level.GetSubLevelIndex();
            inf.remoteTime = Session.CurrentTimeStamp;


            if (e.spr != null)
            {
                inf.PosVector = new PosVector(e.cx, e.cy, e.xr, e.yr, e.dir);
                var sinfo = GetSpriteInfo(e.spr);
                inf.MainSprite = sinfo;
                FillSpriteInfo(e.spr, inf.GUID, sinfo);
                FillEntityAnimInfo(inf, e.spr);
                FillEntityGlowkeyData(e, inf);
            }
        }

    }
}
