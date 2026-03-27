using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class WorldDeltaMsg
{
    /// <summary>When true, client should clear state before applying (initial snapshot).</summary>
    [Key(0)] public bool IsSnapshot { get; set; }
    [Key(1)] public long FromTick { get; set; }
    [Key(2)] public long ToTick { get; set; }
    [Key(3)] public PlayerStateMsg? PlayerState { get; set; }
    [Key(4)] public TileUpdateMsg[] TileUpdates { get; set; } = [];
    [Key(5)] public ChunkDataMsg[] Chunks { get; set; } = [];
    [Key(6)] public CombatEventMsg[] CombatEvents { get; set; } = [];
    [Key(7)] public EntityUpdateMsg[] EntityUpdates { get; set; } = [];
    [Key(8)] public EntityPositionHealthMsg[] EntityPositionHealthUpdates { get; set; } = [];
    [Key(9)] public EntityRemovedMsg[] EntityRemovals { get; set; } = [];
}
