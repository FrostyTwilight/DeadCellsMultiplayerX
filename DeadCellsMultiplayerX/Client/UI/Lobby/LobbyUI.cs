using System;
using System.Threading.Tasks;
using dc.pr;
using dc.ui;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.UI.Common;
using DeadCellsMultiplayerX.Client.UI.Lobby.Controllers;
using DeadCellsMultiplayerX.Client.UI.Lobby.Views;

namespace DeadCellsMultiplayerX.Client.UI.Lobby
{
    //入口
    internal class LobbyUI : UIProcess
    {
        private readonly ClientMain client;
        private readonly LobbyContext ctx = new();
        private readonly LobbyStateMachine state = new();
        public event Action? OnCloseRequested;

        private readonly HostController host;
        private readonly GuestController guest;
        private readonly LobbyView view;

        public LobbyState State => state.Current;
        public LobbyContext Context => ctx;

        private double latencyTimer;
        private bool latencyInFlight;
        private const double LatencyIntervalSec = 2.0;

        public LobbyUI(ClientMain client, TitleScreen titleScreen, Process? parent = null)
            : base(parent)
        {
            this.client = client;

            host = new HostController(client, ctx, state);
            guest = new GuestController(client, ctx, state);
            view = new LobbyView(this, titleScreen, this);

            state.OnChanged += OnStateChanged;

            view.Show();
            state.ForceGo(LobbyState.MainMenu);

            titleScreen.controller.manualLock = true;
            titleScreen.blur(default, default);
        }

        public Task OnCreateRoom(string ip, int port) => host.CreateRoom(ip, port);
        public Task OnJoinRoom(string ip, int port) => guest.JoinRoom(ip, port);
        public Task OnStartGame() => host.StartGame();
        public void OnToggleReady() => guest.ToggleReady();

        public void OnRequestClose()
        {
            OnCloseRequested?.Invoke();
        }

        public void OnLeaveRoom()
        {
            if (ctx.IsHost) host.DestroyRoom();
            else guest.LeaveRoom();
        }

        public override void update()
        {
            base.update();
            if (IsDisposed) return;

            RefreshContext();
            TickLatency();
            view.Refresh(ctx, State);
        }

        private void TickLatency()
        {
            if (latencyInFlight) return;

            latencyTimer += 1.0 / 60.0;
            if (latencyTimer < LatencyIntervalSec) return;
            latencyTimer = 0;

            var gc = client.CurrentGuestClient;
            if (gc == null) return;

            latencyInFlight = true;
            _ = PingAsync(gc);
        }

        private async Task PingAsync(GuestClient gc)
        {
            try
            {
                long ms = await gc.Ping();
                ctx.LatencyMs = ms;
            }
            finally
            {
                latencyInFlight = false;
            }
        }

        private void RefreshContext()
        {
            var lobby = ctx.IsHost
                ? client.CurrentHostClient?.LobbyInfo
                : client.CurrentGuestClient?.LobbyInfo;

            ctx.Lobby = lobby;

            var gc = client.CurrentGuestClient;
            if (gc == null || lobby == null)
            {
                ctx.Me = null;
                return;
            }

            ctx.Me = lobby.Guests.TryGetValue(gc.Guid, out var me) ? me : null;
        }

        private void OnStateChanged(LobbyState from, LobbyState to)
        {
            Logger.Information($"LobbyState: {from} -> {to}");
            view.OnEnterState(to, ctx);
        }

        public void OnGoCreateRoom() => state.ForceGo(LobbyState.CreateRoom);
        public void OnGoJoinRoom() => state.ForceGo(LobbyState.JoinRoom);
        public void OnBackToMain() => state.ForceGo(LobbyState.MainMenu);
        public void OnReturnToMain()
        {
            ctx.Reset();
            state.ForceGo(LobbyState.MainMenu);
        }
        public void OnGotoDebug() => state.ForceGo(LobbyState.Debug);

        protected override void MyDispose()
        {
            state.OnChanged -= OnStateChanged;
            OnCloseRequested = null;

            host.Dispose();
            guest.Dispose();
            view.Dispose();
        }
    }
}