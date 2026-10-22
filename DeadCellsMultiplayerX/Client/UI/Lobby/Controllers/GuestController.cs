using System;
using System.Threading.Tasks;

namespace DeadCellsMultiplayerX.Client.UI.Lobby.Controllers
{
    internal class GuestController : LobbyController
    {
        public GuestController(
            ClientMain client,
            LobbyContext ctx,
            LobbyStateMachine state) : base(client, ctx, state) { }

        //加入房间
        public Task JoinRoom(string ip, int port)
            => RunAsync(async ct =>
            {
                if (!State.TryGo(LobbyState.Connecting)) return;

                Ctx.HostIp = ip;
                Ctx.HostPort = port;
                Ctx.IsHost = false;

                await Client.StartGuest(ip, port).WaitAsync(ct);

                var guest = Client.CurrentGuestClient
                    ?? throw new InvalidOperationException("Guest client not created");

                Ctx.LocalGuid = guest.Guid;
                Ctx.Lobby = guest.LobbyInfo;

                if (!State.TryGo(LobbyState.InRoom)) return;
            }, "JoinRoom");

        //客户端准备
        public void ToggleReady()
        {
            if (IsDisposed) return;
            if (State.Current != LobbyState.InRoom) return;

            var gc = Client.CurrentGuestClient;
            if (gc == null) return;

            var me = Ctx.Me;
            if (me == null) return;

            bool newReady = !me.IsReady;
            gc.SetReady(newReady);
            me.IsReady = newReady;
        }

        //离开
        public void LeaveRoom()
        {
            Client.CurrentGuestClient?.Quit();
            Ctx.Reset();

            if (State.CanGo(LobbyState.MainMenu))
                State.TryGo(LobbyState.MainMenu);
        }

        protected override void MyDispose() { }
    }
}