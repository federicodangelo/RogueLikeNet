using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class SaveGameResponseMsg
{
    [Key(0)] public byte Action { get; set; }
    [Key(1)] public bool Success { get; set; }
    [Key(2)] public string Message { get; set; } = "";
    [Key(3)] public SaveSlotInfoMsg[] Slots { get; set; } = [];
    [Key(4)] public string CurrentSlotId { get; set; } = "";
}

[MessagePackObject]
public class SaveSlotInfoMsg
{
    [Key(0)] public string SlotId { get; set; } = "";
    [Key(1)] public string Name { get; set; } = "";
    [Key(2)] public long Seed { get; set; }
    [Key(3)] public string GeneratorId { get; set; } = "";
    [Key(4)] public long CreatedAtUnixMs { get; set; }
    [Key(5)] public long LastSavedAtUnixMs { get; set; }
}
