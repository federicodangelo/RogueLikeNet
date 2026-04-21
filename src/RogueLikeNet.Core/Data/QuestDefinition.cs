using System.Text.Json.Serialization;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Type of a single quest objective. All objectives in a quest must complete
/// before the player can turn it in to the giver NPC.
/// </summary>
public enum QuestObjectiveType
{
    /// <summary>Kill N monsters of a specific NpcDefinition id.</summary>
    Kill = 0,
    /// <summary>Hold N of an item in inventory (live count, ebbs and flows).</summary>
    Collect = 1,
    /// <summary>Deliver N of an item to the giver NPC (consumed on turn-in).</summary>
    Deliver = 2,
    /// <summary>Reach a location in a specific biome.</summary>
    Reach = 3,
    /// <summary>Gather N drops from a specific ResourceNodeDefinition id.</summary>
    Gather = 4,
    /// <summary>Craft N of an item.</summary>
    Craft = 5,
    /// <summary>Harvest N of a crop item.</summary>
    Harvest = 6,
}

/// <summary>
/// A single objective within a quest. The <see cref="TargetId"/> field
/// references a different registry depending on <see cref="Type"/>:
/// Kill → NpcRegistry, Collect/Deliver/Craft/Harvest → ItemRegistry,
/// Reach → BiomeRegistry (string biome id), Gather → ResourceNodeRegistry.
/// </summary>
public sealed class QuestObjective
{
    public QuestObjectiveType Type { get; set; }
    public string TargetId { get; set; } = "";
    public int Count { get; set; } = 1;
    public string Description { get; set; } = "";

    [JsonIgnore]
    public int TargetNumericId { get; set; }
}

public sealed class QuestRewardItem
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; } = 1;

    [JsonIgnore]
    public int ItemNumericId { get; set; }
}

public sealed class QuestReward
{
    public int Experience { get; set; }
    public int Gold { get; set; }
    public QuestRewardItem[] Items { get; set; } = [];
}

/// <summary>
/// Static definition of a quest loaded from data/quests/*.json.
/// Bound to giver NPCs by <see cref="GiverRole"/>; a specific NPC instance
/// records as the giver when the player accepts the quest.
/// </summary>
public sealed class QuestDefinition : BaseDefinition
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Shown after a successful turn-in.</summary>
    public string CompletionText { get; set; } = "";
    /// <summary>Shown in the offer dialogue if the player declines.</summary>
    public string DeclineText { get; set; } = "";
    /// <summary>Role of NPCs that can offer and accept turn-in for this quest.</summary>
    public TownNpcRole GiverRole { get; set; }
    /// <summary>Minimum player level required to see this quest offered.</summary>
    public int MinPlayerLevel { get; set; } = 1;
    /// <summary>Reserved for future quest chains. MVP: always empty.</summary>
    public string[] PrerequisiteQuestIds { get; set; } = [];
    public QuestObjective[] Objectives { get; set; } = [];
    public QuestReward Rewards { get; set; } = new();

    [JsonIgnore]
    public int[] PrerequisiteQuestNumericIds { get; set; } = [];
}
