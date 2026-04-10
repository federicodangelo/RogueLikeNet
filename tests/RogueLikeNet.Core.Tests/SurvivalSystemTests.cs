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

    // ── Thirst tests ──

    [Fact]
    public void Survival_InitializesAtMaxThirst()
    {
        var s = Survival.Default();
        Assert.Equal(s.MaxThirst, s.Thirst);
    }

    [Fact]
    public void Thirst_DecreasesOverTime()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int initialThirst = player.Survival.Thirst;
        int decayRate = player.Survival.ThirstDecayRate;

        for (int i = 0; i < decayRate; i++)
            engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(initialThirst - 1, player.Survival.Thirst);
    }

    [Fact]
    public void Thirst_DoesNotGoBelowZero()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Survival.Thirst = 1;
        player.Survival.ThirstDecayRate = 1;

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(0, player.Survival.Thirst);

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(0, player.Survival.Thirst);
    }

    [Fact]
    public void Dehydration_DealsDamageAtZeroThirst()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int initialHp = player.Health.Current;
        player.Survival.Thirst = 0;
        player.Survival.ThirstDecayRate = 1;

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.True(player.Health.Current < initialHp);
    }

    [Fact]
    public void IsDehydrated_TrueWhenThirstBelowThreshold()
    {
        var s = Survival.Default();
        s.Thirst = 19;
        Assert.True(s.IsDehydrated);

        s.Thirst = 20;
        Assert.False(s.IsDehydrated);
    }

    [Fact]
    public void IsThirsty_TrueWhenThirstBelowThreshold()
    {
        var s = Survival.Default();
        s.Thirst = 49;
        Assert.True(s.IsThirsty);

        s.Thirst = 50;
        Assert.False(s.IsThirsty);
    }

    [Fact]
    public void PlayerStateData_IncludesThirst()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        var state = engine.GetPlayerStateData(player);
        Assert.NotNull(state);
        Assert.Equal(player.Survival.MaxThirst, state.Thirst);
        Assert.Equal(player.Survival.MaxThirst, state.MaxThirst);
    }

    // ── Threshold effects tests ──

    [Fact]
    public void SpeedPenalty_AppliedWhenHungry()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Set hunger to hungry range (20-50)
        player.Survival.Hunger = 30;
        player.Survival.HungerDecayRate = 99999; // prevent natural decay

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.True(player.ActiveEffects.HasEffect(EffectType.Hungry));
        Assert.True(player.ActiveEffects.CombinedSpeedMultiplierBase100 < 100);
    }

    [Fact]
    public void SpeedPenalty_AppliedWhenThirsty()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Set thirst to thirsty range (20-50)
        player.Survival.Thirst = 30;
        player.Survival.ThirstDecayRate = 99999;

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.True(player.ActiveEffects.HasEffect(EffectType.Thirsty));
        Assert.True(player.ActiveEffects.CombinedSpeedMultiplierBase100 < 100);
    }

    [Fact]
    public void SpeedPenalty_RemovedWhenWellFed()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Full hunger and thirst
        player.Survival.Hunger = 100;
        player.Survival.Thirst = 100;
        player.Survival.HungerDecayRate = 99999;
        player.Survival.ThirstDecayRate = 99999;

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(0, player.ActiveEffects.Count);
        Assert.Equal(100, player.ActiveEffects.CombinedSpeedMultiplierBase100);
    }

    [Fact]
    public void SpeedPenalty_StacksWhenBothHungryAndThirsty()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Both hungry and thirsty
        player.Survival.Hunger = 30;
        player.Survival.Thirst = 30;
        player.Survival.HungerDecayRate = 99999;
        player.Survival.ThirstDecayRate = 99999;

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        Assert.Equal(2, player.ActiveEffects.Count);
        Assert.True(player.ActiveEffects.HasEffect(EffectType.Hungry));
        Assert.True(player.ActiveEffects.HasEffect(EffectType.Thirsty));
        // 50 * 50 / 100 = 25
        Assert.Equal(25, player.ActiveEffects.CombinedSpeedMultiplierBase100);
    }

    [Fact]
    public void WellFed_Properties()
    {
        var s = Survival.Default();
        s.Hunger = 81;
        s.Thirst = 81;
        Assert.True(s.IsWellFed);
        Assert.True(s.IsWellHydrated);

        s.Hunger = 80;
        s.Thirst = 80;
        Assert.False(s.IsWellFed);
        Assert.False(s.IsWellHydrated);
    }
}
