using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;

namespace DeadCellsMultiplayerX.Common.Data
{
    [MessagePackObject]
    public class SpriteInfo
    {
        [Key(0)]public string GUID { get; set; } = Guid.NewGuid().ToString();
        [Key(1)] public string? Parent { get; set; }
        [Key(2)] public string AtlasName { get; set; } = "";
        [Key(3)] public string GroupName { get; set; } = "";
        [Key(4)] public byte[] PivotData { get; set; } = [];
        [Key(5)] public List<SpriteInfo> Children { get; set; } = [];
    }
}
