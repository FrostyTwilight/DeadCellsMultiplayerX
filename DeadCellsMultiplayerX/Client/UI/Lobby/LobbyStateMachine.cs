using System;
using System.Collections.Generic;

namespace DeadCellsMultiplayerX.Client.UI.Lobby
{
    //状态机
    internal class LobbyStateMachine
    {
        public LobbyState Current { get; private set; } = LobbyState.MainMenu;
        public LobbyState Previous { get; private set; } = LobbyState.MainMenu;

        public event Action<LobbyState, LobbyState>? OnChanged;

        private static readonly Dictionary<LobbyState, HashSet<LobbyState>> Rules = new()
        {
            [LobbyState.MainMenu] = [LobbyState.CreateRoom, LobbyState.JoinRoom, LobbyState.Debug],
            [LobbyState.CreateRoom] = [LobbyState.MainMenu, LobbyState.Connecting, LobbyState.Error],
            [LobbyState.JoinRoom] = [LobbyState.MainMenu, LobbyState.Connecting, LobbyState.Error],
            [LobbyState.Debug] = [LobbyState.MainMenu, LobbyState.Connecting,
                                 LobbyState.InRoom, LobbyState.Error],
            [LobbyState.Connecting] = [LobbyState.InRoom, LobbyState.Error,
                                 LobbyState.MainMenu, LobbyState.CreateRoom, LobbyState.JoinRoom],
            [LobbyState.InRoom] = [LobbyState.StartingGame, LobbyState.MainMenu, LobbyState.Error],
            [LobbyState.StartingGame] = [LobbyState.InGame, LobbyState.Error],
            [LobbyState.InGame] = [LobbyState.MainMenu],
            [LobbyState.Error] = [LobbyState.MainMenu, LobbyState.Connecting, LobbyState.Debug],
        };

        public bool CanGo(LobbyState next)
            => Rules.TryGetValue(Current, out var set) && set.Contains(next);

        public bool TryGo(LobbyState next)
        {
            if (!CanGo(next))
            {
                ModEntry.Instance.Logger.Warning($"Invalid transition {Current} -> {next}");
                return false;
            }

            Previous = Current;
            Current = next;
            OnChanged?.Invoke(Previous, Current);
            return true;
        }

        //强制跳转
        public void ForceGo(LobbyState next)
        {
            Previous = Current;
            Current = next;
            OnChanged?.Invoke(Previous, Current);
        }
    }
}