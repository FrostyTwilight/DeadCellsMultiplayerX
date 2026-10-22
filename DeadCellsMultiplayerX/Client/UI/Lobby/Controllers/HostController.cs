using System;
using System.Linq;
using System.Threading.Tasks;

namespace DeadCellsMultiplayerX.Client.UI.Lobby.Controllers
{
    internal class HostController : LobbyController
    {
        public HostController(
            ClientMain client,
            LobbyContext ctx,
            LobbyStateMachine state) : base(client, ctx, state) { }

        //创建房间
        public Task CreateRoom(string ip, int port)
            => RunAsync(async ct =>
            {
                if (!State.TryGo(LobbyState.Connecting)) return;

                Ctx.HostIp = ip;
                Ctx.HostPort = port;
                Ctx.IsHost = true;

                await Client.StartHost(ip, port).WaitAsync(ct);

                var guest = Client.CurrentGuestClient
                    ?? throw new InvalidOperationException("Host guest client not created");

                guest.SetReady(true);
                Ctx.LocalGuid = guest.Guid;
                Ctx.Lobby = guest.LobbyInfo;

                if (!State.TryGo(LobbyState.InRoom)) return;
            }, "CreateRoom");

        //房主开始游戏
        public Task StartGame()
            => RunAsync(async ct =>
            {
                if (!CanStartGame(out var reason))
                {
                    Fail(reason);
                    return;
                }

                if (!State.TryGo(LobbyState.StartingGame)) return;

                var hc = Client.CurrentHostClient
                    ?? throw new InvalidOperationException("Host client not found");

                await hc.StartGame().WaitAsync(ct);
            }, "StartGame");

        private bool CanStartGame(out string reason)
        {
            reason = string.Empty;

            var players = Client.CurrentGuestClient?.LobbyInfo?.Guests;
            if (players == null)
            {
                reason = "连接异常";
                return false;
            }

            var unready = players.Values
                .Where(p => !p.IsReady)
                .Select(p => p.Name)
                .ToArray();

            if (unready.Length > 0)
            {
                reason = $"玩家 {string.Join(",", unready)} 未准备";
                return false;
            }

            return true;
        }

        public void DestroyRoom()
        {
            Client.CurrentHostClient?.Dispose();
            Client.CurrentGuestClient?.Quit();

            Ctx.Reset();

            if (State.CanGo(LobbyState.MainMenu))
                State.TryGo(LobbyState.MainMenu);
        }

        protected override void MyDispose() { }
    }
}