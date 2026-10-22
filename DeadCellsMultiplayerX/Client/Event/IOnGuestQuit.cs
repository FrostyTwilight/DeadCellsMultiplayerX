using ModCore.Events;
using System;
using System.Collections.Generic;
using System.Text;
using DeadCellsMultiplayerX.Client.Host;

namespace DeadCellsMultiplayerX.Client.Event
{
    /// <summary>
    /// 播报房主退出房间事件
    /// </summary>
    /// <remarks> 
    /// 挂载至:<see cref="GuestConnection.MyDispose"/>
    /// </remarks>
    [Event]
    internal interface IOnGuestQuit
    {
        public void OnGuestQuit(GuestInfo guest);
    }
}
