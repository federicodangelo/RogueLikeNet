using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class SaveGameCommandMsg
{
    /// <summary>0=List, 1=New, 2=Load, 3=Delete, 4=Save</summary>
    [Key(0)] public byte Action { get; set; }
    [Key(1)] public string SlotId { get; set; } = "";
    [Key(2)] public string SlotName { get; set; } = "";
    [Key(3)] public long Seed { get; set; }
    [Key(4)] public string GeneratorId { get; set; } = "";
}

public static class SaveGameAction
{
    public const byte List = 0;
    public const byte New = 1;
    public const byte Load = 2;
    public const byte Delete = 3;
    public const byte Save = 4;
}
