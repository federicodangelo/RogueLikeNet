namespace RogueLikeNet.Core.Components;

/// <summary>
/// Progress record for a single objective within an active quest.
/// </summary>
public struct ObjectiveProgress
{
    public int Current;
    public int Target;
    public readonly bool IsComplete => Current >= Target;
}

/// <summary>
/// An active quest instance on a specific player, bound to the NPC that offered it.
/// Progress is tracked per-objective; all objectives must complete before turn-in.
/// </summary>
public sealed class ActiveQuest
{
    public int QuestNumericId;
    /// <summary>Entity id of the NPC who gave this quest. Only that NPC can accept the turn-in.</summary>
    public int GiverEntityId;
    /// <summary>Display name of the giver NPC captured at accept-time, so it still
    /// renders in the quest log even when the NPC's chunk is unloaded.</summary>
    public string GiverName = "";
    /// <summary>World X of the town the giver belongs to (captured at accept-time).
    /// Used for wayfinding arrows when the giver's chunk is unloaded.</summary>
    public int TownX;
    /// <summary>World Y of the town the giver belongs to.</summary>
    public int TownY;
    /// <summary>World Z of the town (same Z as the giver at accept-time).</summary>
    public int TownZ;
    public ObjectiveProgress[] Objectives = [];

    public bool AllObjectivesComplete
    {
        get
        {
            for (int i = 0; i < Objectives.Length; i++)
                if (!Objectives[i].IsComplete) return false;
            return Objectives.Length > 0;
        }
    }
}

/// <summary>
/// Per-player quest state: active quests (with per-objective progress) and
/// numeric ids of quests already completed (for one-shot gating).
/// </summary>
public struct PlayerQuests
{
    public const int MaxActive = 8;

    public List<ActiveQuest> ActiveQuests;
    public List<int> CompletedQuestIds;

    public static PlayerQuests Empty() => new()
    {
        ActiveQuests = new List<ActiveQuest>(),
        CompletedQuestIds = new List<int>(),
    };

    public readonly bool HasActive(int questNumericId)
    {
        if (ActiveQuests == null) return false;
        for (int i = 0; i < ActiveQuests.Count; i++)
            if (ActiveQuests[i].QuestNumericId == questNumericId) return true;
        return false;
    }

    public readonly bool HasCompleted(int questNumericId)
    {
        if (CompletedQuestIds == null) return false;
        for (int i = 0; i < CompletedQuestIds.Count; i++)
            if (CompletedQuestIds[i] == questNumericId) return true;
        return false;
    }

    public readonly ActiveQuest? GetActive(int questNumericId)
    {
        if (ActiveQuests == null) return null;
        for (int i = 0; i < ActiveQuests.Count; i++)
            if (ActiveQuests[i].QuestNumericId == questNumericId) return ActiveQuests[i];
        return null;
    }

    public readonly int ActiveCount => ActiveQuests?.Count ?? 0;
    public readonly bool AtCapacity => ActiveCount >= MaxActive;
}
