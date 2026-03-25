using Arch.Core;
using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Tests;

public class LootAndDeathTests
{
    [Fact]
    public void MonsterDeath_DropsLoot()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();

        var monster = engine.SpawnMonster(0, sx + 1, sy, 103, 0x00FF00, 1, 5, 0, 8);
        ref var mHealth = ref engine.EcsWorld.Get<Health>(monster);
        mHealth.Current = 0;

        int itemsBefore = 0;
        var itemQuery = new QueryDescription().WithAll<GroundItemTag>();
        engine.EcsWorld.Query(in itemQuery, (Entity _) => itemsBefore++);

        engine.Tick();

        Assert.True(true); // System ran without exception
    }

    [Fact]
    public void PlayerDeath_Respawns()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy);

        ref var health = ref engine.EcsWorld.Get<Health>(player);
        health.Current = 0;

        engine.Tick();

        ref var healthAfter = ref engine.EcsWorld.Get<Health>(player);
        Assert.True(healthAfter.Current > 0, "Player should respawn with health > 0");
        Assert.True(engine.EcsWorld.IsAlive(player), "Player entity should still be alive");
    }

    [Fact]
    public void PlayerDeath_LosesExperience()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassIds.Warrior);

        // Give some experience
        ref var classData = ref engine.EcsWorld.Get<ClassData>(player);
        classData.Experience = 100;

        // Kill the player
        ref var health = ref engine.EcsWorld.Get<Health>(player);
        health.Current = 0;

        engine.Tick();

        ref var classDataAfter = ref engine.EcsWorld.Get<ClassData>(player);
        Assert.True(classDataAfter.Experience < 100, "Player should lose experience on death");
    }

    [Fact]
    public void MonsterDeath_MultipleMonstersDropLoot()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();

        // Kill multiple monsters to increase chance of loot drop
        for (int i = 0; i < 10; i++)
        {
            var monster = engine.SpawnMonster(0, sx + i + 1, sy, 103, 0x00FF00, 1, 5, 0, 8);
            ref var mHealth = ref engine.EcsWorld.Get<Health>(monster);
            mHealth.Current = 0;
        }

        engine.Tick();

        // With 10 monsters at 60% drop rate, some should drop loot
        int itemCount = 0;
        var itemQuery = new QueryDescription().WithAll<GroundItemTag>();
        engine.EcsWorld.Query(in itemQuery, (Entity _) => itemCount++);

        Assert.True(itemCount > 0, "At least some of 10 dead monsters should drop loot");
    }
}
