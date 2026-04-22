using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

/// <summary>
/// Per-objective progress info for active quests and offer previews.
/// </summary>
[MessagePackObject]
public class QuestObjectiveInfoMsg
{
    [Key(0)] public int Type { get; set; }
    [Key(1)] public int TargetNumericId { get; set; }
    [Key(2)] public int Current { get; set; }
    [Key(3)] public int Target { get; set; }
    [Key(4)] public string Description { get; set; } = "";
}

[MessagePackObject]
public class QuestRewardInfoMsg
{
    [Key(0)] public int Experience { get; set; }
    [Key(1)] public int Gold { get; set; }
    [Key(2)] public ItemDataMsg[] Items { get; set; } = [];
}

/// <summary>
/// A quest that an NPC is offering to the player right now.
/// </summary>
[MessagePackObject]
public class QuestOfferMsg
{
    [Key(0)] public int QuestNumericId { get; set; }
    [Key(1)] public string QuestStringId { get; set; } = "";
    [Key(2)] public string Title { get; set; } = "";
    [Key(3)] public string Description { get; set; } = "";
    [Key(4)] public QuestObjectiveInfoMsg[] Objectives { get; set; } = [];
    [Key(5)] public QuestRewardInfoMsg? Rewards { get; set; }
}

/// <summary>
/// An active quest the player can turn in at the current NPC.
/// </summary>
[MessagePackObject]
public class QuestTurnInMsg
{
    [Key(0)] public int QuestNumericId { get; set; }
    [Key(1)] public string Title { get; set; } = "";
    [Key(2)] public string CompletionText { get; set; } = "";
    [Key(3)] public bool IsComplete { get; set; }
    [Key(4)] public QuestObjectiveInfoMsg[] Objectives { get; set; } = [];
    [Key(5)] public QuestRewardInfoMsg? Rewards { get; set; }
}

/// <summary>
/// Progress info for a single active quest (shared via PlayerStateMsg for HUD & quest log).
/// </summary>
[MessagePackObject]
public class ActiveQuestInfoMsg
{
    [Key(0)] public int QuestNumericId { get; set; }
    [Key(1)] public string Title { get; set; } = "";
    [Key(2)] public int GiverEntityId { get; set; }
    [Key(3)] public int TownX { get; set; }
    [Key(4)] public int TownY { get; set; }
    [Key(5)] public int TownZ { get; set; }
    [Key(6)] public QuestObjectiveInfoMsg[] Objectives { get; set; } = [];
    [Key(7)] public string GiverName { get; set; } = "";
}

[MessagePackObject]
public class PlayerQuestStateMsg
{
    [Key(0)] public ActiveQuestInfoMsg[] Active { get; set; } = [];
    [Key(1)] public int[] CompletedQuestIds { get; set; } = [];
    /// <summary>
    /// Entity IDs of loaded NPCs that currently have at least one quest offer
    /// available to this player. Used for world-space "!" indicators.
    /// </summary>
    [Key(2)] public int[] QuestGiverEntityIds { get; set; } = [];
}
