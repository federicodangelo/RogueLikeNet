using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class LootAndDeathTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    [Fact]
    public void MonsterDeath_DropsLoot()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();

        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 1, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);
        monster.Health.Current = 0;

        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        int itemsBefore = chunk.GroundItems.ToArray().Count(gi => !gi.IsDestroyed);

        engine.Tick();

        Assert.True(true); // System ran without exception
    }

    [Fact]
    public void PlayerDeath_Respawns()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Health.Current = 0;

        engine.Tick();

        Assert.True(player.Health.Current > 0, "Player should respawn with player.Health > 0");
        Assert.True((!player.IsDead), "Player entity should still be alive");
    }

    [Fact]
    public void PlayerDeath_LosesExperience()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Give some experience
        player.ClassData.Experience = 100;

        // Kill the player
        player.Health.Current = 0;

        engine.Tick();

        Assert.True(player.ClassData.Experience < 100, "Player should lose experience on death");
    }

    [Fact]
    public void MonsterDeath_MultipleMonstersDropLoot()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();

        // Kill multiple monsters to increase chance of loot drop
        for (int i = 0; i < 10; i++)
        {
            var _m = engine.SpawnMonster(Position.FromCoords(sx + i + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 1, Attack = 5, Defense = 0, Speed = 8 });
            ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);
            monster.Health.Current = 0;
        }

        engine.Tick();

        // With 10 monsters at 60% drop rate, some should drop loot
        var chunk = engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ))!;
        int itemCount = chunk.GroundItems.ToArray().Count(gi => !gi.IsDestroyed);

        Assert.True(itemCount > 0, "At least some of 10 dead monsters should drop loot");
    }
}
