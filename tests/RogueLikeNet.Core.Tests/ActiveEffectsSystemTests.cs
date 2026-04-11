using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class ActiveEffectsSystemTests
{
    private static WorldMap CreateMapWithPlayer(int hunger, int thirst, out int playerId)
    {
        var map = new WorldMap(42);
        playerId = 1;
        var player = new PlayerEntity(playerId)
        {
            Position = Position.FromCoords(0, 0, Position.DefaultZ),
            Health = new Health { Current = 100, Max = 100 },
            Survival = new Survival(hunger, Survival.DefaultDecayRate, thirst, Survival.DefaultThirstDecayRate),
        };
        map.AddPlayer(player);
        return map;
    }

    [Fact]
    public void Update_HungryPlayer_AddsHungryEffect()
    {
        // Hungry = Hunger < 50, not starving (>= 20)
        var map = CreateMapWithPlayer(30, 100, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        Assert.True(player.ActiveEffects.HasEffect(EffectType.Hungry));
        Assert.False(player.ActiveEffects.HasEffect(EffectType.Thirsty));
    }

    [Fact]
    public void Update_ThirstyPlayer_AddsThirstyEffect()
    {
        // Thirsty = Thirst < 50, not dehydrated (>= 20)
        var map = CreateMapWithPlayer(100, 30, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        Assert.False(player.ActiveEffects.HasEffect(EffectType.Hungry));
        Assert.True(player.ActiveEffects.HasEffect(EffectType.Thirsty));
    }

    [Fact]
    public void Update_BothHungryAndThirsty_AddsBothEffects()
    {
        var map = CreateMapWithPlayer(30, 30, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        Assert.True(player.ActiveEffects.HasEffect(EffectType.Hungry));
        Assert.True(player.ActiveEffects.HasEffect(EffectType.Thirsty));
        Assert.Equal(2, player.ActiveEffects.Count);
    }

    [Fact]
    public void Update_WellFedPlayer_NoEffects()
    {
        var map = CreateMapWithPlayer(100, 100, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        Assert.Equal(0, player.ActiveEffects.Count);
    }

    [Fact]
    public void Update_StarvingPlayer_NoHungryEffect()
    {
        // Starving = Hunger < 20 → IsHungry is true but IsStarving is also true
        // ApplySurvivalEffects only adds Hungry when IsHungry && !IsStarving
        var map = CreateMapWithPlayer(10, 100, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        Assert.False(player.ActiveEffects.HasEffect(EffectType.Hungry));
    }

    [Fact]
    public void Update_DehydratedPlayer_NoThirstyEffect()
    {
        var map = CreateMapWithPlayer(100, 10, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        Assert.False(player.ActiveEffects.HasEffect(EffectType.Thirsty));
    }

    [Fact]
    public void Update_DeadPlayer_SkipsEffects()
    {
        var map = CreateMapWithPlayer(30, 30, out int id);
        ref var player = ref map.GetPlayerRef(id);
        player.Health = new Health { Current = 0, Max = 100 };

        var system = new ActiveEffectsSystem();
        system.Update(map);

        ref var p = ref map.GetPlayerRef(id);
        Assert.Equal(0, p.ActiveEffects.Count);
    }

    [Fact]
    public void Update_ClearsPreviousEffects()
    {
        var map = CreateMapWithPlayer(30, 100, out int id);
        var system = new ActiveEffectsSystem();

        // First update — should add hungry
        system.Update(map);
        ref var player = ref map.GetPlayerRef(id);
        Assert.Equal(1, player.ActiveEffects.Count);

        // Now feed the player
        player.Survival.Hunger = 100;
        system.Update(map);

        ref var p2 = ref map.GetPlayerRef(id);
        Assert.Equal(0, p2.ActiveEffects.Count);
    }

    [Fact]
    public void Update_HungryEffectHasSpeedMultiplier50()
    {
        var map = CreateMapWithPlayer(30, 100, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        Assert.Equal(50, player.ActiveEffects.CombinedSpeedMultiplierBase100);
    }

    [Fact]
    public void Update_BothEffects_SpeedMultiplierStacks()
    {
        var map = CreateMapWithPlayer(30, 30, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        // 50 * 50 / 100 = 25
        Assert.Equal(25, player.ActiveEffects.CombinedSpeedMultiplierBase100);
    }
}
