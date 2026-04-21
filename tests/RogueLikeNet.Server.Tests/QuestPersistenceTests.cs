using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Server.Tests;

public class QuestPersistenceTests
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
        throw new InvalidOperationException($"No quest with objective type {type} found.");
    }

    [Fact]
    public void PlayerQuests_RoundTripsThroughPlayerSerializer()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        int playerId = p.Id;
        ref var player = ref engine.WorldMap.GetPlayerRef(playerId);

        var quest = FindQuestWithObjective(QuestObjectiveType.Kill);
        var killObj = quest.Objectives.First(o => o.Type == QuestObjectiveType.Kill);
        player.Quests.ActiveQuests!.Add(new ActiveQuest
        {
            QuestNumericId = quest.NumericId,
            GiverEntityId = 7,
            GiverChunkX = 1,
            GiverChunkY = 2,
            GiverChunkZ = 3,
            Objectives = new[] { new ObjectiveProgress { Current = 2, Target = killObj.Count } },
        });

        // Completed list should preserve real quest ids.
        var quest2 = GameData.Instance.Quests.All.First(q => q.NumericId != quest.NumericId);
        player.Quests.CompletedQuestIds!.Add(quest2.NumericId);

        var save = PlayerSerializer.SerializePlayer(player, "tester");
        Assert.NotEqual("{}", save.QuestsJson);

        using var engine2 = CreateEngine();
        ref var restored = ref PlayerSerializer.RestorePlayer(engine2, 2, save);
        // Note: RestorePlayer uses connectionId=2; restored entity id allocated by engine2.

        Assert.Equal(1, restored.Quests.ActiveCount);
        Assert.True(restored.Quests.HasActive(quest.NumericId));
        Assert.True(restored.Quests.HasCompleted(quest2.NumericId));

        var active = restored.Quests.GetActive(quest.NumericId)!;
        Assert.Equal(7, active.GiverEntityId);
        Assert.Equal(1, active.GiverChunkX);
        Assert.Equal(2, active.GiverChunkY);
        Assert.Equal(3, active.GiverChunkZ);
        Assert.Equal(2, active.Objectives[0].Current);
        Assert.Equal(killObj.Count, active.Objectives[0].Target);
    }

    [Fact]
    public void RestoreQuests_DropsUnknownQuestIds()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        int playerId = p.Id;
        ref var player = ref engine.WorldMap.GetPlayerRef(playerId);

        // Fabricate an unknown quest id in both active and completed lists.
        player.Quests.ActiveQuests!.Add(new ActiveQuest
        {
            QuestNumericId = 123456789,
            GiverEntityId = 1,
            Objectives = new[] { new ObjectiveProgress { Current = 1, Target = 3 } },
        });
        player.Quests.CompletedQuestIds!.Add(987654321);

        var save = PlayerSerializer.SerializePlayer(player, "tester");

        using var engine2 = CreateEngine();
        ref var restored = ref PlayerSerializer.RestorePlayer(engine2, 2, save);

        Assert.Equal(0, restored.Quests.ActiveCount);
        Assert.Empty(restored.Quests.CompletedQuestIds!);
    }
}
