using System;
using System.Threading.Tasks;
using dc.pr;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using DeadCellsMultiplayerX.Client.UI.Lobby;

namespace DeadCellsMultiplayerX.Client.UI
{
    internal class LobbyMenuX : UIProcess
    {
        private readonly LobbyUI lobby;

        public LobbyMenuX(ClientMain client, TitleScreen titleScreen) : base(titleScreen, dc.Const.Class.ROOT_DP_MENU)
        {
            lobby = new LobbyUI(client, titleScreen, this);
            lobby.OnCloseRequested += OnLobbyCloseRequested;
        }

        public override void update()
        {
            base.update();
        }

        private void OnLobbyCloseRequested()
        {
            Dispose(); 
        }

        protected override void MyDispose()
        {
            if (lobby != null)
            {
                lobby.OnCloseRequested -= OnLobbyCloseRequested;
                lobby.Dispose();
            }
            TitleScreen.Class.ME?.unblur();
            TitleScreen.Class.ME?.controller.manualLock = false;
        }
    }
}