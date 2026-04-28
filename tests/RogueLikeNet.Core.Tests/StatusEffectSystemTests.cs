using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class StatusEffectSystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private static GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    private static int SpellId(string id) => GameData.Instance.Spells.GetNumericId(id);
    private static int NpcId(string id) => GameData.Instance.Npcs.GetNumericId(id);

    [Fact]
    public void FireDamage_AppliesBurningAndTicksDamage()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        var monster = engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = NpcId("goblin"), Health = 200, Attack = 0, Defense = 0, Speed = 0 });

        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = SpellId("fireball");
        engine.Tick();

        ref var monsterAfterImpact = ref engine.WorldMap.GetMonsterRef(monster.Id);
        int healthAfterImpact = monsterAfterImpact.Health.Current;
        Assert.True(monsterAfterImpact.StatusEffects.HasEffect(StatusEffectType.Burning));
        Assert.Equal(StatusEffectType.Burning, engine.Spells.LastTickEvents[0].StatusEffectType);

        bool sawBurnTick = false;
        for (int i = 0; i < 20; i++)
        {
            engine.Tick();
            if (engine.StatusEffects.LastTickEvents.Any(e => e.StatusEffectType == StatusEffectType.Burning && e.Damage > 0))
                sawBurnTick = true;
        }

        ref var monsterAfterBurn = ref engine.WorldMap.GetMonsterRef(monster.Id);
        Assert.True(monsterAfterBurn.Health.Current < healthAfterImpact);
        Assert.True(sawBurnTick);
    }

    [Fact]
    public void IceDamage_AppliesChillAndSlowsMonsterDelays()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);
        int baseMoveInterval = Math.Max(0, 10 - 4);
        int baseAttackInterval = Math.Max(0, 10 - 1);

        var monster = engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = NpcId("goblin"), Health = 200, Attack = 0, Defense = 0, Speed = 4, AttackSpeed = 1 });

        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = SpellId("ice_bolt");
        engine.Tick();

        ref var chilled = ref engine.WorldMap.GetMonsterRef(monster.Id);
        Assert.True(chilled.StatusEffects.HasEffect(StatusEffectType.Chilled));
        Assert.True(chilled.MoveDelay.Interval > baseMoveInterval);
        Assert.True(chilled.AttackDelay.Interval > baseAttackInterval);
        Assert.Equal(StatusEffectType.Chilled, engine.Spells.LastTickEvents[0].StatusEffectType);
    }

    [Fact]
    public void MonsterAttackDelay_UsesAttackSpeed_NotMoveSpeed()
    {
        using var engine = CreateEngine();
        var monster = engine.SpawnMonster(Position.FromCoords(5, 5, Position.DefaultZ),
            new MonsterData { MonsterTypeId = NpcId("goblin"), Health = 50, Attack = 5, Defense = 1, Speed = 4, AttackSpeed = 1 });

        ref var monsterRef = ref engine.WorldMap.GetMonsterRef(monster.Id);
        Assert.Equal(Math.Max(0, 10 - 4), monsterRef.MoveDelay.Interval);
        Assert.Equal(Math.Max(0, 10 - 1), monsterRef.AttackDelay.Interval);
    }

    [Fact]
    public void StatusDamage_KillsMonster_GrantsKillActionEvent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = NpcId("goblin"), Health = 26, Attack = 0, Defense = 0, Speed = 0 });

        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = SpellId("fireball");
        engine.Tick();

        bool sawStatusKill = false;
        for (int i = 0; i < 25; i++)
        {
            engine.Tick();
            if (engine.StatusEffects.LastTickEvents.Any(e => e.TargetDied))
            {
                sawStatusKill = true;
                break;
            }
        }

        Assert.True(sawStatusKill);

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Contains(player.ActionEvents, e => e.EventType == PlayerActionEventType.Kill);
    }
}
