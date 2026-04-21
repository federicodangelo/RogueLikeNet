using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class QuestSystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private static GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    private static QuestDefinition FindQuestWithObjective(QuestObjectiveType type)
    {
        foreach (var q in GameData.Instance.Quests.All)
            foreach (var o in q.Objectives)
                if (o.Type == type) return q;
        throw new InvalidOperationException($"No quest with objective type {type} found in loaded data.");
    }

    [Fact]
    public void QuestRegistry_LoadsQuests_WithResolvedTargetIds()
    {
        var registry = GameData.Instance.Quests;
        Assert.True(registry.All.Count >= 10, $"Expected 10+ quests, got {registry.All.Count}");

        foreach (var quest in registry.All)
        {
            Assert.NotEmpty(quest.Objectives);
            foreach (var obj in quest.Objectives)
            {
                Assert.NotEqual(0, obj.TargetNumericId);
                Assert.True(obj.Count > 0);
            }
        }
    }

    [Fact]
    public void AcceptQuest_WhenGiverInRange_AddsToActiveQuests()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var quest = FindQuestWithObjective(QuestObjectiveType.Kill);

        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        int playerId = p.Id;
        var npcData = engine.SpawnTownNpc(Position.FromCoords(sx + 1, sy, Position.DefaultZ), "Giver", sx, sy, 5, quest.GiverRole);
        int npcId = npcData.Id;

        ref var player = ref engine.WorldMap.GetPlayerRef(playerId);
        player.ClassData.Level = Math.Max(player.ClassData.Level, quest.MinPlayerLevel);
        player.Input.ActionType = ActionTypes.AcceptQuest;
        player.Input.TargetNpcEntityId = npcId;
        player.Input.TargetQuestId = quest.NumericId;
        engine.Tick();

        ref var after = ref engine.WorldMap.GetPlayerRef(playerId);
        Assert.Equal(1, after.Quests.ActiveCount);
        Assert.True(after.Quests.HasActive(quest.NumericId));
        var active = after.Quests.GetActive(quest.NumericId)!;
        Assert.Equal(npcId, active.GiverEntityId);
        Assert.Contains(after.ActionEvents, e => e.EventType == PlayerActionEventType.QuestAccepted && e.QuestNumericId == quest.NumericId);
    }

    [Fact]
    public void AcceptQuest_WhenNotNearGiver_Fails()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var quest = FindQuestWithObjective(QuestObjectiveType.Kill);

        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        int playerId = p.Id;
        var npcData = engine.SpawnTownNpc(Position.FromCoords(sx + 10, sy + 10, Position.DefaultZ), "Giver", sx, sy, 5, quest.GiverRole);
        int npcId = npcData.Id;

        ref var player = ref engine.WorldMap.GetPlayerRef(playerId);
        player.ClassData.Level = Math.Max(player.ClassData.Level, quest.MinPlayerLevel);
        player.Input.ActionType = ActionTypes.AcceptQuest;
        player.Input.TargetNpcEntityId = npcId;
        player.Input.TargetQuestId = quest.NumericId;
        engine.Tick();

        ref var after = ref engine.WorldMap.GetPlayerRef(playerId);
        Assert.Equal(0, after.Quests.ActiveCount);
        Assert.Contains(after.ActionEvents, e => e.EventType == PlayerActionEventType.QuestActionFailed && e.FailReason == ActionFailReason.QuestWrongGiver);
    }

    [Fact]
    public void KillObjective_AdvancesOnMonsterKill()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        var quest = FindQuestWithObjective(QuestObjectiveType.Kill);
        var killObj = quest.Objectives.First(o => o.Type == QuestObjectiveType.Kill);

        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        int playerId = p.Id;
        ref var player = ref engine.WorldMap.GetPlayerRef(playerId);

        player.Quests.ActiveQuests!.Add(new ActiveQuest
        {
            QuestNumericId = quest.NumericId,
            GiverEntityId = 999,
            Objectives = new[] { new ObjectiveProgress { Current = 0, Target = killObj.Count } },
        });

        player.ActionEvents.Add(new PlayerActionEvent
        {
            EventType = PlayerActionEventType.Kill,
            KilledNpcTypeId = killObj.TargetNumericId,
        });
        new QuestSystem().Update(engine.WorldMap, engine);

        ref var after = ref engine.WorldMap.GetPlayerRef(playerId);
        var active = after.Quests.GetActive(quest.NumericId)!;
        Assert.Equal(1, active.Objectives[0].Current);
    }

    [Fact]
    public void CollectObjective_TracksLiveInventoryCount()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        var quest = FindQuestWithObjective(QuestObjectiveType.Collect);
        var collectObj = quest.Objectives.First(o => o.Type == QuestObjectiveType.Collect);

        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        int playerId = p.Id;
        ref var player = ref engine.WorldMap.GetPlayerRef(playerId);

        player.Quests.ActiveQuests!.Add(new ActiveQuest
        {
            QuestNumericId = quest.NumericId,
            GiverEntityId = 999,
            Objectives = new[] { new ObjectiveProgress { Current = 0, Target = collectObj.Count } },
        });

        player.Inventory.Items.Add(new ItemData { ItemTypeId = collectObj.TargetNumericId, StackCount = collectObj.Count });

        new QuestSystem().Update(engine.WorldMap, engine);

        ref var after = ref engine.WorldMap.GetPlayerRef(playerId);
        var active = after.Quests.GetActive(quest.NumericId)!;
        Assert.True(active.Objectives[0].IsComplete);
    }

    [Fact]
    public void TurnInQuest_WhenComplete_GrantsRewardsAndMovesToCompleted()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        var quest = FindQuestWithObjective(QuestObjectiveType.Kill);
        var killObj = quest.Objectives.First(o => o.Type == QuestObjectiveType.Kill);

        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        int playerId = p.Id;
        var npcData = engine.SpawnTownNpc(Position.FromCoords(sx + 1, sy, Position.DefaultZ), "Giver", sx, sy, 5, quest.GiverRole);
        int npcId = npcData.Id;

        ref var player = ref engine.WorldMap.GetPlayerRef(playerId);

        player.Quests.ActiveQuests!.Add(new ActiveQuest
        {
            QuestNumericId = quest.NumericId,
            GiverEntityId = npcId,
            Objectives = new[] { new ObjectiveProgress { Current = killObj.Count, Target = killObj.Count } },
        });

        foreach (var obj in quest.Objectives)
        {
            if (obj.Type == QuestObjectiveType.Deliver)
                player.Inventory.Items.Add(new ItemData { ItemTypeId = obj.TargetNumericId, StackCount = obj.Count });
        }

        int xpBefore = player.ClassData.Experience;
        player.Input.ActionType = ActionTypes.TurnInQuest;
        player.Input.TargetNpcEntityId = npcId;
        player.Input.TargetQuestId = quest.NumericId;
        engine.Tick();

        ref var after = ref engine.WorldMap.GetPlayerRef(playerId);
        Assert.Equal(0, after.Quests.ActiveCount);
        Assert.True(after.Quests.HasCompleted(quest.NumericId));
        Assert.Contains(after.ActionEvents, e => e.EventType == PlayerActionEventType.QuestCompleted && e.QuestNumericId == quest.NumericId);
        if (quest.Rewards.Experience > 0)
            Assert.True(after.ClassData.Experience > xpBefore);
    }

    [Fact]
    public void TurnInQuest_WithWrongNpc_Fails()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        var quest = FindQuestWithObjective(QuestObjectiveType.Kill);
        var killObj = quest.Objectives.First(o => o.Type == QuestObjectiveType.Kill);

        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        int playerId = p.Id;
        var giver = engine.SpawnTownNpc(Position.FromCoords(sx + 1, sy, Position.DefaultZ), "Giver", sx, sy, 5, quest.GiverRole);
        var other = engine.SpawnTownNpc(Position.FromCoords(sx - 1, sy, Position.DefaultZ), "Other", sx, sy, 5, quest.GiverRole);
        int giverId = giver.Id;
        int otherId = other.Id;

        ref var player = ref engine.WorldMap.GetPlayerRef(playerId);
        player.Quests.ActiveQuests!.Add(new ActiveQuest
        {
            QuestNumericId = quest.NumericId,
            GiverEntityId = giverId,
            Objectives = new[] { new ObjectiveProgress { Current = killObj.Count, Target = killObj.Count } },
        });

        player.Input.ActionType = ActionTypes.TurnInQuest;
        player.Input.TargetNpcEntityId = otherId;
        player.Input.TargetQuestId = quest.NumericId;
        engine.Tick();

        ref var after = ref engine.WorldMap.GetPlayerRef(playerId);
        Assert.Equal(1, after.Quests.ActiveCount);
        Assert.Contains(after.ActionEvents, e => e.EventType == PlayerActionEventType.QuestActionFailed && e.FailReason == ActionFailReason.QuestWrongGiver);
    }

    [Fact]
    public void AbandonQuest_RemovesFromActiveList()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        var quest = FindQuestWithObjective(QuestObjectiveType.Kill);

        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        int playerId = p.Id;
        ref var player = ref engine.WorldMap.GetPlayerRef(playerId);

        player.Quests.ActiveQuests!.Add(new ActiveQuest
        {
            QuestNumericId = quest.NumericId,
            GiverEntityId = 999,
            Objectives = new[] { new ObjectiveProgress { Current = 0, Target = 5 } },
        });

        player.Input.ActionType = ActionTypes.AbandonQuest;
        player.Input.TargetQuestId = quest.NumericId;
        engine.Tick();

        ref var after = ref engine.WorldMap.GetPlayerRef(playerId);
        Assert.Equal(0, after.Quests.ActiveCount);
        Assert.False(after.Quests.HasCompleted(quest.NumericId));
        Assert.Contains(after.ActionEvents, e => e.EventType == PlayerActionEventType.QuestAbandoned);
    }
}
