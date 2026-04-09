using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class SurvivalSystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    [Fact]
    public void Survival_InitializesAtMaxHunger()
    {
        var s = Survival.Default();
        Assert.Equal(s.MaxHunger, s.Hunger);
    }

    [Fact]
    public void Hunger_DecreasesOverTime()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int initialHunger = player.Survival.Hunger;
        int decayRate = player.Survival.HungerDecayRate;

        // Tick enough times for one hunger point to decay
        for (int i = 0; i < decayRate; i++)
            engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(initialHunger - 1, player.Survival.Hunger);
    }

    [Fact]
    public void Hunger_DoesNotGoBelowZero()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Force hunger to 1 with fast decay
        player.Survival.Hunger = 1;
        player.Survival.HungerDecayRate = 1;

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(0, player.Survival.Hunger);

        // Tick again — should stay at 0
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(0, player.Survival.Hunger);
    }

    [Fact]
    public void Starvation_DealsDamageAtZeroHunger()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int initialHp = player.Health.Current;
        player.Survival.Hunger = 0;
        player.Survival.HungerDecayRate = 1;

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.True(player.Health.Current < initialHp);
    }

    [Fact]
    public void IsStarving_TrueWhenHungerBelowThreshold()
    {
        var s = Survival.Default();
        s.Hunger = 19;
        Assert.True(s.IsStarving);

        s.Hunger = 20;
        Assert.False(s.IsStarving);
    }

    [Fact]
    public void IsHungry_TrueWhenHungerBelowThreshold()
    {
        var s = Survival.Default();
        s.Hunger = 49;
        Assert.True(s.IsHungry);

        s.Hunger = 50;
        Assert.False(s.IsHungry);
    }

    [Fact]
    public void PlayerStateData_IncludesHunger()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var state = engine.GetPlayerStateData(player);
        Assert.NotNull(state);
        Assert.Equal(player.Survival.MaxHunger, state.Hunger);
        Assert.Equal(player.Survival.MaxHunger, state.MaxHunger);
    }
}
