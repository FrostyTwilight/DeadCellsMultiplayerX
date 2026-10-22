using System.Runtime.CompilerServices;
using dc;
using dc.en;
using dc.hl.types;
using DeadCellsMultiplayerX.Common.Data;
using Hashlink.Virtuals;
using ModCore.Utilities;

namespace DeadCellsMultiplayerX.Common.Serializers
{
    public static class AffectCodec
    {
        private static readonly ConditionalWeakTable<Entity, Dictionary<int, double>> startCache = new();

        public static List<AffectEntry> Collect(Entity hero, double snapSec, double baseFps)
        {
            var result = new List<AffectEntry>();
            var affects = hero.affects;
            if (affects == null) return result;

            if (!startCache.TryGetValue(hero, out var t0map))
            {
                t0map = new Dictionary<int, double>();
                startCache.Add(hero, t0map);
            }

            var seen = new HashSet<int>();

            for (int i = 0; i < affects.length; i++)
            {
                var inner = affects.array[i] as ArrayObj;

                if (inner == null) continue;

                for (int j = 0; j < inner.length; j++)
                {
                    var v = inner.array[j] as virtual_a_t_uniqId_val_;

                    if (v == null) continue;

                    int id = v.uniqId;
                    seen.Add(id);

                    if (!t0map.TryGetValue(id, out double t0))
                    {
                        t0 = snapSec;
                        t0map[id] = t0;
                    }

                    result.Add(new AffectEntry
                    {
                        OuterIndex = i,
                        InnerIndex = j,
                        A = v.a,
                        T = v.t,
                        UniqId = id,
                        Val = v.val,
                        StartSec = t0,
                        EndSec = t0 + v.t / baseFps,
                    });
                }
            }

            // 清理
            var stale = new List<int>();
            foreach (var k in t0map.Keys)
                if (!seen.Contains(k)) stale.Add(k);
            foreach (var k in stale) t0map.Remove(k);

            return result;
        }


        public static void Apply(Entity hero, List<AffectEntry> entries, double renderSec)
        {
            if (entries == null || entries.Count == 0) return;

            var affects = hero.affects;
            if (affects == null) return;

            foreach (var e in entries)
            {
                if (renderSec < e.StartSec || renderSec >= e.EndSec)
                    continue;

                while (affects.length <= e.OuterIndex)
                    affects.push((ArrayObj)ArrayUtils.CreateDyn().array);

                if (!(affects.array[e.OuterIndex] is ArrayObj inner))
                {
                    inner = (ArrayObj)ArrayUtils.CreateDyn().array;
                    affects.array[e.OuterIndex] = inner;
                }

                while (inner.length <= e.InnerIndex)
                    inner.push(null);

                inner.array[e.InnerIndex] = new virtual_a_t_uniqId_val_
                {
                    a = e.A,
                    t = e.T,
                    uniqId = e.UniqId,
                    val = e.Val
                };
            }
        }
    }
}