using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DeadCellsMultiplayerX.Common.Data
{
    /// <summary>
    /// 用于大关卡切换时发送的数据
    /// </summary>
    public class LevelVote
    {
        public string PlyerID = string.Empty;

        public string NextLevelID = "PrisonStart";

        public bool LevelSelected = false;
    }
}