using DeadCellsMultiplayerX.Client;
using IngameDebugConsole;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeadCellsMultiplayerX
{
    internal static class DebugConsoleCommands
    {
        private static async void HoldAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "");
            }
        }

        [ConsoleMethod("dcmp-start-host", "启动 Host")]
        public static void StartHost(TextWriter writer, string ip = "127.0.0.1", int port = 44567)
        {
            HoldAsync(ClientMain.Instance.StartHost(ip, port));
        }

        [ConsoleMethod("dcmp-start-guest", "启动 Guest")]
        public static void ConnectLobby(TextWriter writer, string ip, int port)
        {
            HoldAsync(ClientMain.Instance.StartGuest(ip, port));
        }

        [ConsoleMethod("dcmp-quit-lobby", "离开房间")]
        public static void QuitLobby(TextWriter writer)
        {
            ClientMain.Instance.CurrentGuestClient!.Quit();
        }

        [ConsoleMethod("dcmp-set-name", "修改玩家名称")]
        public static void SetName(TextWriter writer, string name)
        {
            ClientMain.Instance.CurrentGuestClient!.SetName(name);
        }

#if DEBUG
        [ConsoleMethod("dcmp-worldframe-drop-server-frame", "丢弃下一个服务端世界帧以测试客户端 NACK")]
        public static void DropServerFrame(TextWriter writer, int count = 1)
        {
            WorldFrameDebug.DropServerFrames += Math.Max(1, count);
            writer.WriteLine($"Will drop {WorldFrameDebug.DropServerFrames} server frame(s).");
        }

        [ConsoleMethod("dcmp-worldframe-skip-client-upload", "跳过下一次客户端上传以测试服务端 NACK")]
        public static void SkipClientUpload(TextWriter writer, int count = 1)
        {
            WorldFrameDebug.SkipClientUploads += Math.Max(1, count);
            writer.WriteLine($"Will skip {WorldFrameDebug.SkipClientUploads} client upload(s).");
        }

        [ConsoleMethod("dcmp-worldframe-force-resync", "强制客户端请求全量世界重同步")]
        public static void ForceResync(TextWriter writer)
        {
            WorldFrameDebug.ForceResync = true;
            writer.WriteLine("World resync will be requested on next tick.");
        }

        [ConsoleMethod("dcmp-worldframe-stats", "打印客户端世界帧统计")]
        public static void WorldFrameStats(TextWriter writer)
        {
            var manager = WorldFrameDebug.ClientManager;
            if (manager == null)
            {
                writer.WriteLine("WorldFrameManager not available.");
                return;
            }

            writer.WriteLine($"epoch={manager.Epoch} baseline={manager.HasBaseline} " +
                             $"lastSeq={manager.LastDeliveredSequence} pending={manager.PendingCount}");
            writer.WriteLine($"gap={manager.GapCount} nack={manager.NackSent} " +
                             $"bundle={manager.BundleReceived} skipped={manager.SkippedFrames} " +
                             $"resync={manager.ResyncCount} droppedOld={manager.DroppedOld} " +
                             $"overflow={manager.PendingOverflow}");
        }
#endif
    }
}
