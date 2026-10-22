#if DEBUG
using System.Diagnostics;
using System.Runtime.InteropServices;
using DeadCellsMultiplayerX.Client;
using Serilog;

namespace DeadCellsMultiplayerX
{
    internal static class DebugLauncher
    {
        private const string ExePath =
            @"D:\steam\steamapps\common\Dead Cells\coremod\core\host\startup\DeadCellsModding.exe";

        private static ILogger? MyLogger;
        private static ILogger logger => MyLogger ??= Log.ForContext(nameof(DebugLauncher), nameof(DebugLauncher));


        private static string? autoAddr;
        private static string? playerName;

        public static bool IsTestClient => !string.IsNullOrEmpty(autoAddr);

        public static void Init()
        {
            var addr = Environment.GetEnvironmentVariable("DCMP_AUTOCONNECT");
            var name = Environment.GetEnvironmentVariable("DCMP_PLAYERNAME");

            if (!string.IsNullOrEmpty(addr))
            {
                autoAddr = addr;
                logger.Information("auto-connect (env): {autoAddr}", autoAddr);
            }
            if (!string.IsNullOrEmpty(name))
            {
                playerName = name;
                logger.Information("player name (env): {playerName}", playerName);
            }

            foreach (var a in Environment.GetCommandLineArgs())
            {
                if (a.StartsWith("--dcmp-autoconnect="))
                {
                    autoAddr = a.Substring("--dcmp-autoconnect=".Length);
                }
                else if (a.StartsWith("--dcmp-playername="))
                {
                    playerName = a.Substring("--dcmp-playername=".Length);
                }
            }
        }

        public static void StartTest(int clients = 2, string ip = "127.0.0.1", int port = 44567)
        {
            HoldAsync(StartTestAsync(clients, ip, port));
        }

        private static async Task StartTestAsync(int clients, string ip, int port)
        {
            DebugFlags.ClearViaAnsi();

            logger.Information("Host starting on {f1}:{f2}", ip, port);

            await ClientMain.Instance.StartHost(ip, port);

            await Task.Delay(500);

            var gc = ClientMain.Instance.CurrentGuestClient;
            if (gc == null) return;

            gc.SetReady(true);

            await Task.Delay(500);
            for (int i = 0; i < clients; i++)
                LaunchClient(ip, port, i);

            logger.Information("Launched {f1} clients", clients);

            await AutoStartWhenReady(clients, timeoutMs: 120_000);

        }

        private static void LaunchClient(string ip, int port, int idx)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ExePath,
                UseShellExecute = false,
            };

            psi.EnvironmentVariables["DCMP_AUTOCONNECT"] = $"{ip}:{port}";
            psi.EnvironmentVariables["DCMP_PLAYERNAME"] = $"TestPlayer{idx + 1}";

            psi.EnvironmentVariables.Remove("DCCM_MULTIPLAYER_NO_CLIENT");


            var p = Process.Start(psi);
            logger.Information("Launched PID={f1} as TestPlayer{f2}", p?.Id, idx + 1);
        }


        private static async Task AutoStartWhenReady(int clientCount, int timeoutMs)
        {
            int expected = 1 + clientCount;
            var sw = Stopwatch.StartNew();

            int lastCount = -1;
            int lastReady = -1;

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                await Task.Delay(500);

                var hc = ClientMain.Instance.CurrentHostClient;
                if (hc == null) continue;

                var guests = hc.LobbyInfo?.Guests;
                if (guests == null) continue;

                int count = guests.Count;
                int readyCount = 0;
                foreach (var kv in guests)
                {
                    if (kv.Value.IsReady) readyCount++;
                }

                if (count != lastCount || readyCount != lastReady)
                {
                    lastCount = count;
                    lastReady = readyCount;
                    logger.Information(
                        "{f1}/{f2} players, {f3} ready, " +
                        "CanStart={f4}", count, expected, readyCount, hc.CanStartGame);
                }
                if (count >= expected && readyCount >= expected && hc.CanStartGame)
                {
                    logger.Information("{Source} All ready, starting game...", nameof(DebugLauncher));
                    await hc.StartGame();
                    return;
                }
            }
        }

        public static void TriggerAutoJoin()
        {
            if (!IsTestClient) return;

            var parts = autoAddr!.Split(':');
            if (parts.Length < 2) return;

            string ip = parts[0];
            int port = int.Parse(parts[1]);

            logger.Information(
                "Auto-joining {f1}:{f2} as {f3}", ip, port, playerName);

            autoAddr = null;

            HoldAsync(JoinAndReadyAsync(ip, port));
        }

        private static async Task JoinAndReadyAsync(string ip, int port)
        {
            logger.Information("Starting guest...");
            await ClientMain.Instance.StartGuest(ip, port);

            await Task.Delay(500);

            var gc = ClientMain.Instance.CurrentGuestClient;
            if (gc == null) return;

            await Task.Delay(2000);

            gc.SetReady(true);
        }


        private static async void HoldAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                logger.Error(ex, " HoldAsync failed");
            }
        }
    }
}
#endif