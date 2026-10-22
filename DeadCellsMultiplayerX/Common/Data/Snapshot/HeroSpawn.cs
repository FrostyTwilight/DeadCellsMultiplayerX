using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;

namespace DeadCellsMultiplayerX.Common.Data.Snapshot
{
    /// <summary>
    /// 初始化hero
    /// </summary>
    [MessagePackObject]
    public class HeroSpawn
    {   
        /// <summary>
        /// 客户id
        /// </summary>
        [Key(0)] public string GUID = string.Empty;

        /// <summary>
        /// 记录所操控的实体,用于后续变身模式
        /// </summary>
        [Key(1)] public string TypeName = string.Empty;

        /// <summary>
        /// 皮肤贴图名
        /// </summary>
        [Key(2)] public string ColorMapModel = string.Empty;

        /// <summary>
        /// 色带图
        /// </summary>
        [Key(3)] public string ColorMapSkin = string.Empty;

        /// <summary>
        /// 当前所在的子关卡id
        /// </summary>
        [Key(4)] public int SubLevelId = -1;

        /// <summary>
        /// 贴图状态
        /// </summary>
        [Key(5)] public SpriteInfo MainSprite = new();

        /// <summary>
        /// 皮肤发光数据
        /// </summary>
        [Key(6)] public Dictionary<int, byte[]> GlowData = [];

        /// <summary>
        /// 当前的关卡id
        /// </summary>
        [Key(7)] public string LevelId = string.Empty;

        /// <summary>
        /// 服务端分配,静态数据变化时递增
        /// </summary>
        [Key(8)] public uint Version;


        /// <summary>
        /// 判断静态部分是否变化，用于决定是否重发 Spawned
        /// </summary>
        public bool SameAs(HeroSpawn o)
        {
            if (o == null) return false;

            return GUID == o.GUID
                && TypeName == o.TypeName
                && ColorMapModel == o.ColorMapModel
                && ColorMapSkin == o.ColorMapSkin
                && SubLevelId == o.SubLevelId
                && SpriteSame(MainSprite, o.MainSprite)
                && GlowSame(GlowData, o.GlowData);
        }

        private static bool SpriteSame(SpriteInfo a, SpriteInfo b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            return a.AtlasName == b.AtlasName
                && a.GroupName == b.GroupName
                && ByteEq(a.PivotData, b.PivotData);
        }

        private static bool GlowSame(Dictionary<int, byte[]> a, Dictionary<int, byte[]> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            foreach (var (k, va) in a)
            {
                if (!b.TryGetValue(k, out var vb)) return false;
                if (!ByteEq(va, vb)) return false;
            }
            return true;
        }

        private static bool ByteEq(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;

            return true;
        }
    }
}