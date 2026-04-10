using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class EntityUpdateMsg
{
    [Key(0)] public long Id { get; set; }
    [Key(1)] public int EntityType { get; set; }
    [Key(2)] public int X { get; set; }
    [Key(3)] public int Y { get; set; }
    [Key(4)] public int Z { get; set; }
    [Key(5)] public int GlyphId { get; set; }
    [Key(6)] public int FgColor { get; set; }
    [Key(7)] public int Health { get; set; }
    [Key(8)] public int MaxHealth { get; set; }
    [Key(9)] public int LightRadius { get; set; }
    [Key(10)] public ItemDataMsg? Item { get; set; }


    /// <summary>
    /// Returns true when only X, Y, or Health differ from <paramref name="other"/>.
    /// </summary>
    public bool HasOnlyPositionHealthChanges(EntityUpdateMsg other) =>
        Id == other.Id &&
        EntityType == other.EntityType &&
        GlyphId == other.GlyphId &&
        FgColor == other.FgColor &&
        MaxHealth == other.MaxHealth &&
        LightRadius == other.LightRadius &&
        ItemDataMsg.Equals(Item, other.Item);

    /// <summary>
    /// Value equality
    /// </summary>
    public bool SameValues(EntityUpdateMsg other) =>
        HasOnlyPositionHealthChanges(other) &&
        X == other.X &&
        Y == other.Y &&
        Z == other.Z &&
        Health == other.Health;
}
