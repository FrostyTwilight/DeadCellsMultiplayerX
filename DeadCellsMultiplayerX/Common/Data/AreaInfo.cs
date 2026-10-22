using MessagePack;

namespace DeadCellsMultiplayerX.Common.Data
{
    [MessagePackObject]
    public class AreaInfo
    {
        [Key(0)] public RectInt Rect { get; set; } = new();
        [Key(1)] public int[]? Collision { get; set; }
        [Key(2)] public List<EntityInfo> Entities { get; set; } = [];
    }
}
