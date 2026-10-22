

using ModCore.Events;

namespace DeadCellsMultiplayerX.Server.Events
{
    [Event]
    public interface IOnLobbyMenuDisposed
    {
        void OnLobbyMenuDisposed();
    }
}