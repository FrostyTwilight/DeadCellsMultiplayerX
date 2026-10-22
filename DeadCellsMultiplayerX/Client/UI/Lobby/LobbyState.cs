using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DeadCellsMultiplayerX.Client.UI.Lobby
{
    //状态
    internal enum LobbyState
    {
        MainMenu,      //主页
        CreateRoom,    //创建房间
        JoinRoom,      // 加入房间
        Connecting,     //连接中
        InRoom,     //联机房
        StartingGame,   //开始游戏
        InGame,     //已开始游戏
        Error,      //报错
        Debug 
    }

}