#if DEBUG
using DeadCellsMultiplayerX.Client.Guest.WorldX.Snapshot;

namespace DeadCellsMultiplayerX
{
    /// <summary>
    /// 世界帧调试开关:用于强制制造缺口,验证 NACK/重同步路径。
    /// </summary>
    internal static class WorldFrameDebug
    {
        public static int DropServerFrames;
        public static int SkipClientUploads;
        public static bool ForceResync;
        public static WorldFrameManager? ClientManager;

        public static bool ConsumeDropServerFrame()
        {
            if (DropServerFrames <= 0) return false;
            DropServerFrames--;
            return true;
        }

        public static bool ConsumeSkipClientUpload()
        {
            if (SkipClientUploads <= 0) return false;
            SkipClientUploads--;
            return true;
        }

        public static bool ConsumeForceResync()
        {
            if (!ForceResync) return false;
            ForceResync = false;
            return true;
        }
    }
}
#endif
