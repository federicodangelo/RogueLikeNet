using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

/// <summary>
/// Replaces the old NpcDialogueMsg. Emitted when a player bumps or interacts
/// with a town NPC. Carries flavor text plus any quest offers/turn-ins and
/// whether the NPC has a shop.
/// </summary>
[MessagePackObject]
public class NpcInteractionMsg
{
    [Key(0)] public int NpcEntityId { get; set; }
    [Key(1)] public int NpcX { get; set; }
    [Key(2)] public int NpcY { get; set; }
    [Key(3)] public int NpcZ { get; set; }
    [Key(4)] public string NpcName { get; set; } = "";
    [Key(5)] public int NpcRole { get; set; }
    [Key(6)] public string FlavorText { get; set; } = "";
    [Key(7)] public QuestOfferMsg[] QuestOffers { get; set; } = [];
    [Key(8)] public QuestTurnInMsg[] QuestTurnIns { get; set; } = [];
    [Key(9)] public bool HasShop { get; set; }
}
