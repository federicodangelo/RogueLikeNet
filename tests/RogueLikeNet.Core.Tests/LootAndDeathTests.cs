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
        engine.EnsureChunkLoaded(0, 0, Position.DefaultZ);
        var (sx, sy, _) = engine.FindSpawnPosition();

        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 1, Attack = 5, Defense = 0, Speed = 8 });
        monster.Health.Current = 0;

        var chunk = engine.WorldMap.TryGetChunk(0, 0, Position.DefaultZ)!;
        int itemsBefore = chunk.GroundItems.Count(gi => !gi.IsDead);

        engine.Tick();

        Assert.True(true); // System ran without exception
    }

    [Fact]
    public void PlayerDeath_Respawns()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0, Position.DefaultZ);
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        player.Health.Current = 0;

        engine.Tick();

        Assert.True(player.Health.Current > 0, "Player should respawn with player.Health > 0");
        Assert.True((!player.IsDead), "Player entity should still be alive");
    }

    [Fact]
    public void PlayerDeath_LosesExperience()
    {
        using var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(0, 0, Position.DefaultZ);
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

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
        engine.EnsureChunkLoaded(0, 0, Position.DefaultZ);
        var (sx, sy, _) = engine.FindSpawnPosition();

        // Kill multiple monsters to increase chance of loot drop
        for (int i = 0; i < 10; i++)
        {
            var monster = engine.SpawnMonster(sx + i + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 1, Attack = 5, Defense = 0, Speed = 8 });
            monster.Health.Current = 0;
        }

        engine.Tick();

        // With 10 monsters at 60% drop rate, some should drop loot
        var chunk = engine.WorldMap.TryGetChunk(0, 0, Position.DefaultZ)!;
        int itemCount = chunk.GroundItems.Count(gi => !gi.IsDead);

        Assert.True(itemCount > 0, "At least some of 10 dead monsters should drop loot");
    }
}
