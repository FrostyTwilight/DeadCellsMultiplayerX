using dc;
using dc.h2d;
using dc.h3d;
using dc.h3d.mat;
using dc.h3d.shader;
using dc.hxd;
using dc.libs.heaps.slib;
using dc.shader;
using Hashlink.Virtuals;
using HaxeProxy.Runtime;
using ModCore.Utilities;

namespace DeadCellsMultiplayerX.Client.UI
{
    public static class UIRenderer
    {
        public static HSprite CreateHeroSpr(string skinMould, dc.h2d.Object parent)
        {
            var idle = "idle".AsHaxeString();
            var skinInfo = Cdb.Class.getSkinInfo(skinMould.AsHaxeString())
                .ToVirtual<virtual_colorMap_consoleCmdId_glowData_group_head_incompatibleHeads_item_model_onlyDefaultHead_scarfBlendMode_scarfs_>();
            var lib = Assets.Class.getHeroLib(Cdb.Class.getSkinInfo(skinMould.AsHaxeString()));

            var spr = new HSprite(lib, idle, Ref<int>.Null, null);
            spr.get_anim().play(idle, null, null)?.loop(null).genSpeed = 0.4;
            spr.set_visible(true);
            parent.addChild(spr);

            Res.Class.load("atlas/beheaded_aladdin_s.png".AsHaxeString())
                .toTexture().set_filter(new Filter.Nearest());

            // Shader 管线
            var colorMap = new ColorMap(Assets.Class.getHeroColorMap(skinInfo));
            var normalMap = new dc.shader.NormalMap(lib.getNormalMapFromSprite(spr));
            var dirLight = new DirLighted();

            spr.addShader(colorMap);
            spr.addShader(normalMap);
            spr.addShader(dirLight);

            // 全局光照
            ApplyLightDefaults(spr, dirLight);

            return spr;
        }

        private static void ApplyLightDefaults(HSprite spr, DirLighted light)
        {
            var globals = spr.getScene().ctx.manager.globals;
            var g = light.shader.globals;

            int shadowId = -1, dirId = -1;
            for (int i = 0; i < g.length; i++)
            {
                var c = g.getDyn(i);
                var name = c.v.name.ToString();
                var parent = c.v.parent.name.ToString();
                if (parent != "light") continue;

                if (name == "shadowColor") shadowId = c.globalId;
                if (name == "dirVec") dirId = c.globalId;
                if (shadowId != -1 && dirId != -1) break;
            }

            if (shadowId != -1)
                globals.map.set(shadowId, new Vector(Ref<double>.In(1), Ref<double>.In(1), Ref<double>.In(1), Ref<double>.In(1)));
            if (dirId != -1)
                globals.map.set(dirId, new Vector(Ref<double>.In(0), Ref<double>.In(0), Ref<double>.In(1), Ref<double>.In(1)));
        }
    }
}
