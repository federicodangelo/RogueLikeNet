using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class WorldDeltaMsg
{
    [Key(0)] public long FromTick { get; set; }
    [Key(1)] public long ToTick { get; set; }
    [Key(2)] public TileUpdateMsg[] TileUpdates { get; set; } = [];
    [Key(3)] public EntityUpdateMsg[] EntityUpdates { get; set; } = [];
    [Key(4)] public CombatEventMsg[] CombatEvents { get; set; } = [];
    [Key(5)] public ChunkDataMsg[] Chunks { get; set; } = [];
    [Key(6)] public PlayerStateMsg? PlayerState { get; set; }
    /// <summary>When true, client should clear state before applying (initial snapshot).</summary>
    [Key(7)] public bool IsSnapshot { get; set; }
}
