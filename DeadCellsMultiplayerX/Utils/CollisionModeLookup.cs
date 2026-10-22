using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using dc;

namespace DeadCellsMultiplayerX.Utils
{
    public static class CollisionModeLookup
    {
        private static readonly CollisionMode[] Lookup =
        {
            new CollisionMode.Normal(),        // 0
            new CollisionMode.All(),           // 1
            new CollisionMode.None(),          // 2
            new CollisionMode.Ladder(),        // 3
            new CollisionMode.Pully(),         // 4
            new CollisionMode.WallGrab(),      // 5
            new CollisionMode.IgnoreOneWay(),  // 6
            new CollisionMode.IgnoreWalls(),   // 7
        };

        public static CollisionMode Get(CollisionMode.Indexes index)
        {
            int i = (int)index;
            if ((uint)i >= (uint)Lookup.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "未知的 CollisionMode 索引");
            return Lookup[i];
        }

        public static CollisionMode Get(byte rawIndex)
        {
            if (rawIndex >= Lookup.Length)
                throw new ArgumentOutOfRangeException(nameof(rawIndex), rawIndex, "未知的 CollisionMode 索引");
            return Lookup[rawIndex];
        }

        public static CollisionMode.Indexes GetIndex(CollisionMode mode)
        {
            return mode.Index;
        }
    }

}