using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DeadCellsMultiplayerX.Client.UI.Lobby
{
    /// <summary>
    /// 大厅共享上下文
    /// </summary>
    internal class LobbyContext
    {
        public string LocalGuid = string.Empty;
        public GuestInfo? Me;   //当前的客户
        public LobbyInfo? Lobby; //联机大厅数据
        public bool IsHost; //是否是房主

        public long LatencyMs; //与服务器的延迟
        public string HostIp = string.Empty; //ip
        public int HostPort; //端口
        public string? LastError; //报错文本

        public void Reset()
        {
            Me = null;
            Lobby = null;
            IsHost = false;
            LastError = null;
            LatencyMs = 0;
        }
    }
}