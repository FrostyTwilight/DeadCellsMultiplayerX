using ModCore.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeadCellsMultiplayerX.Client.Event
{
    [Event]
    internal interface IOnLobbyUpdate
    {
        void OnLobbyUpdate();
    }
}
