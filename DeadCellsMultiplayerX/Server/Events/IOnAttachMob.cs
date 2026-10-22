using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModCore.Events;

namespace DeadCellsMultiplayerX.Server.Events
{
    [Event]
    public interface IOnAttachMob
    {
        public record class Data(dc.level.Mob mobdata, dc.en.Mob mob);
        public void OnAttachMob(Data data);
    }
}