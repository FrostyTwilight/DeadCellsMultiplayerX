using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DeadCellsMultiplayerX
{
    internal static class DebugFlags
    {
        private static readonly HashSet<string> flags = new();

        public static void Toggle(string key)
        {
            if (!flags.Remove(key)) flags.Add(key);
        }

        public static bool Get(string key) => flags.Contains(key);

        public static void Set(string key, bool on)
        {
            if (on) flags.Add(key);
            else flags.Remove(key);
        }

        public static void ClearViaAnsi()
        {
            Console.Write("\x1b[2J\x1b[3J\x1b[H");
        }

    }
}