using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes quest objective progression from the tick's <see cref="PlayerActionEvent"/>s,
/// and handles quest-related input actions (accept / turn-in / abandon).
/// Runs after CombatSystem/CraftingSystem/FarmingSystem so their emitted events
/// are visible for objective advancement.
/// </summary>
public class QuestSystem
{
    private const int MaxProximity = 2;

    public void Update(WorldMap worldMap, GameEngine engine)
    {
        foreach (ref var player in worldMap.Players)
        {
            if (player.IsDead) continue;

            // 1) Handle explicit quest input actions
            switch (player.Input.ActionType)
            {
                case ActionTypes.AcceptQuest:
                    ProcessAccept(ref player, worldMap);
                    player.Input.ActionType = ActionTypes.None;
                    break;
                case ActionTypes.TurnInQuest:
                    ProcessTurnIn(ref player, worldMap, engine);
                    player.Input.ActionType = ActionTypes.None;
                    break;
                case ActionTypes.AbandonQuest:
                    ProcessAbandon(ref player);
                    player.Input.ActionType = ActionTypes.None;
                    break;
                case ActionTypes.DeclineQuest:
                    // No server state change; only for UI symmetry.
                    player.Input.ActionType = ActionTypes.None;
                    break;
            }

            // 2) Advance objectives from this tick's action events
            AdvanceFromActionEvents(ref player);

            // 3) Poll collect objectives and live Reach checks
            PollPassiveObjectives(ref player, worldMap);
        }
    }

    private static void ProcessAccept(ref PlayerEntity player, WorldMap worldMap)
    {
        int questId = player.Input.TargetQuestId;
        int npcId = player.Input.TargetNpcEntityId;
        if (questId == 0 || npcId == 0)
        {
            EmitFailed(ref player, questId, ActionFailReason.QuestNotFound);
            return;
        }

        var questDef = GameData.Instance.Quests.Get(questId);
        if (questDef == null)
        {
            EmitFailed(ref player, questId, ActionFailReason.QuestNotFound);
            return;
        }

        if (player.Quests.HasActive(questId) || player.Quests.HasCompleted(questId))
        {
            EmitFailed(ref player, questId, ActionFailReason.QuestAlreadyActive);
            return;
        }

        if (player.ClassData.Level < questDef.MinPlayerLevel)
        {
            EmitFailed(ref player, questId, ActionFailReason.QuestNotAvailable);
            return;
        }

        foreach (var preq in questDef.PrerequisiteQuestNumericIds)
        {
            if (!player.Quests.HasCompleted(preq))
            {
                EmitFailed(ref player, questId, ActionFailReason.QuestNotAvailable);
                return;
            }
        }

        if (player.Quests.AtCapacity)
        {
            EmitFailed(ref player, questId, ActionFailReason.QuestCapacityFull);
            return;
        }

        if (!TryFindGiverNpc(ref player, worldMap, npcId, questDef.GiverRole, out var giverPos, out var giverName, out var townX, out var townY))
        {
            EmitFailed(ref player, questId, ActionFailReason.QuestWrongGiver);
            return;
        }

        var progress = new ObjectiveProgress[questDef.Objectives.Length];
        for (int i = 0; i < progress.Length; i++)
            progress[i] = new ObjectiveProgress { Current = 0, Target = questDef.Objectives[i].Count };

        player.Quests.ActiveQuests!.Add(new ActiveQuest
        {
            QuestNumericId = questId,
            GiverEntityId = npcId,
            GiverName = giverName,
            TownX = townX,
            TownY = townY,
            TownZ = giverPos.Z,
            Objectives = progress,
        });

        player.ActionEvents.Add(new PlayerActionEvent
        {
            EventType = PlayerActionEventType.QuestAccepted,
            QuestNumericId = questId,
        });
    }

    private static void ProcessTurnIn(ref PlayerEntity player, WorldMap worldMap, GameEngine engine)
    {
        int questId = player.Input.TargetQuestId;
        int npcId = player.Input.TargetNpcEntityId;

        var active = player.Quests.GetActive(questId);
        if (active == null)
        {
            EmitFailed(ref player, questId, ActionFailReason.QuestNotFound);
            return;
        }
        if (active.GiverEntityId != npcId)
        {
            EmitFailed(ref player, questId, ActionFailReason.QuestWrongGiver);
            return;
        }

        var questDef = GameData.Instance.Quests.Get(questId);
        if (questDef == null)
        {
            EmitFailed(ref player, questId, ActionFailReason.QuestNotFound);
            return;
        }

        if (!TryFindGiverNpc(ref player, worldMap, npcId, questDef.GiverRole, out var giverPos, out _, out _, out _))
        {
            EmitFailed(ref player, questId, ActionFailReason.QuestTooFar);
            return;
        }

        // Deliver objectives: verify inventory has the items first (without removing).
        for (int i = 0; i < questDef.Objectives.Length; i++)
        {
            var obj = questDef.Objectives[i];
            if (obj.Type != QuestObjectiveType.Deliver) continue;
            if (CountItem(ref player, obj.TargetNumericId) < obj.Count)
            {
                EmitFailed(ref player, questId, ActionFailReason.QuestMissingItems);
                return;
            }
        }

        if (!active.AllObjectivesComplete)
        {
            // Non-deliver objectives must already be complete.
            bool allExceptDeliverDone = true;
            for (int i = 0; i < questDef.Objectives.Length; i++)
            {
                if (questDef.Objectives[i].Type == QuestObjectiveType.Deliver) continue;
                if (!active.Objectives[i].IsComplete) { allExceptDeliverDone = false; break; }
            }
            if (!allExceptDeliverDone)
            {
                EmitFailed(ref player, questId, ActionFailReason.QuestNotComplete);
                return;
            }
        }

        // Consume deliver items now that everything is validated.
        for (int i = 0; i < questDef.Objectives.Length; i++)
        {
            var obj = questDef.Objectives[i];
            if (obj.Type != QuestObjectiveType.Deliver) continue;
            RemoveItems(ref player, obj.TargetNumericId, obj.Count);
            active.Objectives[i].Current = obj.Count;
        }

        // Grant rewards.
        player.ClassData.Experience += questDef.Rewards.Experience;

        if (questDef.Rewards.Gold > 0)
        {
            int goldId = GameData.Instance.Items.GetNumericId("gold_coin");
            if (goldId != 0)
            {
                var goldData = new ItemData { ItemTypeId = goldId, StackCount = questDef.Rewards.Gold };
                if (!InventorySystem.AddItemToInventory(ref player, goldData))
                    engine.SpawnItemOnGround(goldData, engine.FindDropPosition(giverPos));
            }
        }

        foreach (var rewardItem in questDef.Rewards.Items)
        {
            if (rewardItem.ItemNumericId == 0) continue;
            var data = new ItemData { ItemTypeId = rewardItem.ItemNumericId, StackCount = rewardItem.Count };
            if (!InventorySystem.AddItemToInventory(ref player, data))
                engine.SpawnItemOnGround(data, engine.FindDropPosition(giverPos));
        }

        // Move quest from active → completed.
        player.Quests.ActiveQuests!.Remove(active);
        player.Quests.CompletedQuestIds!.Add(questId);

        player.ActionEvents.Add(new PlayerActionEvent
        {
            EventType = PlayerActionEventType.QuestCompleted,
            QuestNumericId = questId,
        });
    }

    private static void ProcessAbandon(ref PlayerEntity player)
    {
        int questId = player.Input.TargetQuestId;
        var active = player.Quests.GetActive(questId);
        if (active == null)
        {
            EmitFailed(ref player, questId, ActionFailReason.QuestNotFound);
            return;
        }

        player.Quests.ActiveQuests!.Remove(active);
        player.ActionEvents.Add(new PlayerActionEvent
        {
            EventType = PlayerActionEventType.QuestAbandoned,
            QuestNumericId = questId,
        });
    }

    private static void AdvanceFromActionEvents(ref PlayerEntity player)
    {
        if (player.Quests.ActiveQuests == null || player.Quests.ActiveQuests.Count == 0) return;

        // Snapshot count so events we emit below don't get iterated as inputs.
        int origEventCount = player.ActionEvents.Count;
        for (int e = 0; e < origEventCount; e++)
        {
            var evt = player.ActionEvents[e];
            if (evt.Failed) continue;

            foreach (var active in player.Quests.ActiveQuests)
            {
                var questDef = GameData.Instance.Quests.Get(active.QuestNumericId);
                if (questDef == null) continue;

                for (int o = 0; o < questDef.Objectives.Length; o++)
                {
                    ref var progress = ref active.Objectives[o];
                    if (progress.IsComplete) continue;

                    var obj = questDef.Objectives[o];
                    int increment = MatchEvent(obj, evt);
                    if (increment <= 0) continue;

                    int before = progress.Current;
                    progress.Current = Math.Min(progress.Target, progress.Current + increment);
                    if (progress.Current != before)
                    {
                        player.ActionEvents.Add(new PlayerActionEvent
                        {
                            EventType = PlayerActionEventType.QuestObjectiveAdvanced,
                            QuestNumericId = active.QuestNumericId,
                            QuestObjectiveIndex = o,
                            ObjectiveCurrent = progress.Current,
                            ObjectiveTarget = progress.Target,
                        });
                    }
                }
            }
        }
    }

    private static int MatchEvent(QuestObjective obj, PlayerActionEvent evt)
    {
        return obj.Type switch
        {
            QuestObjectiveType.Kill when evt.EventType == PlayerActionEventType.Kill
                && evt.KilledNpcTypeId == obj.TargetNumericId => 1,
            QuestObjectiveType.Craft when evt.EventType == PlayerActionEventType.Craft
                && evt.ItemTypeId == obj.TargetNumericId => Math.Max(1, evt.StackCount),
            QuestObjectiveType.Harvest when evt.EventType == PlayerActionEventType.Harvest
                && evt.ItemTypeId == obj.TargetNumericId => Math.Max(1, evt.StackCount),
            QuestObjectiveType.Gather when evt.EventType == PlayerActionEventType.Gather
                && evt.KilledNpcTypeId == obj.TargetNumericId => 1,
            _ => 0,
        };
    }

    private static void PollPassiveObjectives(ref PlayerEntity player, WorldMap worldMap)
    {
        if (player.Quests.ActiveQuests == null || player.Quests.ActiveQuests.Count == 0) return;

        // Cache current biome numeric id (for Reach objectives).
        int currentBiomeNumericId = 0;
        {
            var chunkPos = Chunk.WorldToChunkCoord(player.Position);
            var biomeType = BiomeRegistry.GetBiomeForChunk(chunkPos, worldMap.Seed);
            var biomeDef = GameData.Instance.Biomes.Get(biomeType);
            if (biomeDef != null) currentBiomeNumericId = biomeDef.NumericId;
        }

        foreach (var active in player.Quests.ActiveQuests)
        {
            var questDef = GameData.Instance.Quests.Get(active.QuestNumericId);
            if (questDef == null) continue;

            for (int o = 0; o < questDef.Objectives.Length; o++)
            {
                ref var progress = ref active.Objectives[o];
                var obj = questDef.Objectives[o];

                int newValue = obj.Type switch
                {
                    QuestObjectiveType.Collect => Math.Min(progress.Target, CountItem(ref player, obj.TargetNumericId)),
                    QuestObjectiveType.Reach when currentBiomeNumericId == obj.TargetNumericId => progress.Target,
                    _ => -1,
                };

                if (newValue < 0) continue;
                if (newValue == progress.Current) continue;

                progress.Current = newValue;
                player.ActionEvents.Add(new PlayerActionEvent
                {
                    EventType = PlayerActionEventType.QuestObjectiveAdvanced,
                    QuestNumericId = active.QuestNumericId,
                    QuestObjectiveIndex = o,
                    ObjectiveCurrent = progress.Current,
                    ObjectiveTarget = progress.Target,
                });
            }
        }
    }

    private static bool TryFindGiverNpc(ref PlayerEntity player, WorldMap worldMap, int npcEntityId, TownNpcRole expectedRole, out Position npcPos, out string npcName, out int townX, out int townY)
    {
        foreach (var chunk in worldMap.LoadedChunks)
        {
            foreach (ref var npc in chunk.TownNpcs)
            {
                if (npc.Id != npcEntityId) continue;
                if (npc.IsDead) { npcPos = default; npcName = ""; townX = 0; townY = 0; return false; }
                if (npc.NpcData.Role != expectedRole) { npcPos = default; npcName = ""; townX = 0; townY = 0; return false; }
                int dx = Math.Abs(npc.Position.X - player.Position.X);
                int dy = Math.Abs(npc.Position.Y - player.Position.Y);
                if (dx > MaxProximity || dy > MaxProximity || npc.Position.Z != player.Position.Z)
                {
                    npcPos = default;
                    npcName = "";
                    townX = 0;
                    townY = 0;
                    return false;
                }
                // Refresh conversation lock so the NPC stays put for any follow-up interactions.
                npc.NpcData.InConversationWith = player.Id;
                npcPos = npc.Position;
                npcName = npc.NpcData.Name ?? "";
                townX = npc.NpcData.TownCenterX;
                townY = npc.NpcData.TownCenterY;
                return true;
            }
        }
        npcPos = default;
        npcName = "";
        townX = 0;
        townY = 0;
        return false;
    }

    private static int CountItem(ref PlayerEntity player, int itemTypeId)
    {
        if (itemTypeId == 0 || player.Inventory.Items == null) return 0;
        int count = 0;
        for (int i = 0; i < player.Inventory.Items.Count; i++)
        {
            if (player.Inventory.Items[i].ItemTypeId == itemTypeId)
                count += player.Inventory.Items[i].StackCount;
        }
        return count;
    }

    private static void RemoveItems(ref PlayerEntity player, int itemTypeId, int amount)
    {
        int remaining = amount;
        for (int i = player.Inventory.Items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            if (player.Inventory.Items[i].ItemTypeId != itemTypeId) continue;
            var item = player.Inventory.Items[i];
            if (item.StackCount <= remaining)
            {
                remaining -= item.StackCount;
                player.Inventory.Items.RemoveAt(i);
                player.QuickSlots.OnItemRemoved(i);
            }
            else
            {
                item.StackCount -= remaining;
                player.Inventory.Items[i] = item;
                remaining = 0;
            }
        }
    }

    private static void EmitFailed(ref PlayerEntity player, int questId, ActionFailReason reason)
    {
        player.ActionEvents.Add(new PlayerActionEvent
        {
            EventType = PlayerActionEventType.QuestActionFailed,
            QuestNumericId = questId,
            Failed = true,
            FailReason = reason,
        });
    }

    /// <summary>
    /// Returns true if the player has at least one quest available to accept from
    /// an NPC of the given role (not already active, not completed, prerequisites met,
    /// player level sufficient, and quest capacity not exceeded).
    /// </summary>
    public static bool HasAvailableOfferForRole(ref PlayerEntity player, TownNpcRole role)
    {
        if (player.Quests.AtCapacity) return false;
        var quests = GameData.Instance.Quests.GetForGiverRole(role);
        if (quests.Count == 0) return false;
        for (int i = 0; i < quests.Count; i++)
        {
            var q = quests[i];
            if (player.ClassData.Level < q.MinPlayerLevel) continue;
            if (player.Quests.HasActive(q.NumericId)) continue;
            if (player.Quests.HasCompleted(q.NumericId)) continue;
            bool prereqOk = true;
            for (int p = 0; p < q.PrerequisiteQuestNumericIds.Length; p++)
            {
                if (!player.Quests.HasCompleted(q.PrerequisiteQuestNumericIds[p])) { prereqOk = false; break; }
            }
            if (!prereqOk) continue;
            return true;
        }
        return false;
    }
}
