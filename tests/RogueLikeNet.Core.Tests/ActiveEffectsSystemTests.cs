using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class ActiveEffectsSystemTests
{
    private const int HungerStarving = 19;
    private const int HungerHungry = 49;
    private const int HungerWellFed = 81;
    private const int ThirstDehydrated = 19;
    private const int ThirstThirsty = 49;
    private const int ThirstWellHydrated = 81;


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
    public void Update_HungryPlayer_NoEffect()
    {
        // Hungry = Hunger < 50, not starving (>= 20)
        var map = CreateMapWithPlayer(HungerHungry, ThirstWellHydrated, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        Assert.False(player.ActiveEffects.HasEffect(EffectType.Hungry));
        Assert.False(player.ActiveEffects.HasEffect(EffectType.Thirsty));
    }

    [Fact]
    public void Update_ThirstyPlayer_NoEffect()
    {
        // Thirsty = Thirst < 50, not dehydrated (>= 20)
        var map = CreateMapWithPlayer(HungerWellFed, ThirstThirsty, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        Assert.False(player.ActiveEffects.HasEffect(EffectType.Hungry));
        Assert.False(player.ActiveEffects.HasEffect(EffectType.Thirsty));
    }

    [Fact]
    public void Update_BothHungryAndThirsty_NoEffect()
    {
        var map = CreateMapWithPlayer(HungerHungry, ThirstThirsty, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        Assert.False(player.ActiveEffects.HasEffect(EffectType.Hungry));
        Assert.False(player.ActiveEffects.HasEffect(EffectType.Thirsty));
    }

    [Fact]
    public void Update_WellFedPlayer_NoEffects()
    {
        var map = CreateMapWithPlayer(HungerWellFed, ThirstWellHydrated, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        Assert.Equal(0, player.ActiveEffects.Count);
    }

    [Fact]
    public void Update_StarvingPlayer_HungryEffect()
    {
        var map = CreateMapWithPlayer(HungerStarving, ThirstWellHydrated, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        Assert.True(player.ActiveEffects.HasEffect(EffectType.Hungry));
    }

    [Fact]
    public void Update_DehydratedPlayer_ThirstyEffect()
    {
        var map = CreateMapWithPlayer(HungerWellFed, ThirstDehydrated, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        Assert.True(player.ActiveEffects.HasEffect(EffectType.Thirsty));
    }

    [Fact]
    public void Update_DeadPlayer_SkipsEffects()
    {
        var map = CreateMapWithPlayer(HungerStarving, ThirstDehydrated, out int id);
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
        var map = CreateMapWithPlayer(HungerStarving, ThirstWellHydrated, out int id);
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
        var map = CreateMapWithPlayer(HungerStarving, ThirstWellHydrated, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        Assert.Equal(75, player.ActiveEffects.CombinedSpeedMultiplierBase100);
    }

    [Fact]
    public void Update_BothEffects_SpeedMultiplierStacks()
    {
        var map = CreateMapWithPlayer(HungerStarving, ThirstDehydrated, out int id);
        var system = new ActiveEffectsSystem();

        system.Update(map);

        ref var player = ref map.GetPlayerRef(id);
        // 75 * 75 / 100 = 50
        Assert.Equal(56, player.ActiveEffects.CombinedSpeedMultiplierBase100);
    }
}
