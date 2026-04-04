using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class SkillSystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(Position.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    [Fact]
    public void Heal_RestoresHealth()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Damage the player
        player.Health.Current = 50;

        // Use Heal skill (slot 1 for Mage)
        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 1; // Heal is slot 1 for Mage
        engine.Tick();

        Assert.True(player.Health.Current > 50, $"Health {player.Health.Current} should be > 50 after Heal");
    }

    [Fact]
    public void Skill_SetsCooldown()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Health.Current = 50;

        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 1; // Heal
        engine.Tick();

        Assert.True(player.Skills.Cooldown1 > 0, "Cooldown should be set after using Heal skill");
    }

    [Fact]
    public void Cooldown_TicksDown()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Health.Current = 50;

        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 1;
        engine.Tick();

        int cdAfterUse = player.Skills.Cooldown1;

        engine.Tick(); // one more tick

        Assert.True(player.Skills.Cooldown1 < cdAfterUse, "Cooldown should decrease each tick");
    }

    [Fact]
    public void PowerStrike_DamagesMonster()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        int hpBefore = monster.Health.Current;

        // PowerStrike is slot 0 for Warrior, target direction (1, 0)
        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.True(monster.Health.Current < hpBefore, $"Monster HP {monster.Health.Current} should be < {hpBefore} after PowerStrike");
    }

    [Fact]
    public void ShieldBash_DamagesMonster()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        int hpBefore = monster.Health.Current;

        // ShieldBash is slot 1 for Warrior
        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 1;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.True(monster.Health.Current < hpBefore, $"Monster HP {monster.Health.Current} should be < {hpBefore} after ShieldBash");
    }

    [Fact]
    public void Backstab_DamagesMonster()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Rogue);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        int hpBefore = monster.Health.Current;

        // Backstab is slot 0 for Rogue
        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.True(monster.Health.Current < hpBefore, $"Monster HP {monster.Health.Current} should be < {hpBefore} after Backstab");
    }

    [Fact]
    public void Dodge_BoostsDefense()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Rogue);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        int defBefore = player.CombatStats.Defense;

        // Dodge is slot 1 for Rogue
        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 1;
        engine.Tick();

        Assert.True(player.CombatStats.Defense > defBefore, $"Defense {player.CombatStats.Defense} should be > {defBefore} after Dodge");
    }

    [Fact]
    public void Fireball_DamagesMonsterInArea()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Spawn monster near the target area
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        int hpBefore = monster.Health.Current;

        // Fireball is slot 0 for Mage, target direction (1, 0)
        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.True(monster.Health.Current < hpBefore, $"Monster HP {monster.Health.Current} should be < {hpBefore} after Fireball");
    }

    [Fact]
    public void PowerShot_DamagesMonsterAtRange()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Spawn monster at distance
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        int hpBefore = monster.Health.Current;

        // PowerShot is slot 0 for Ranger, target direction (3, 0)
        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 3;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.True(monster.Health.Current < hpBefore, $"Monster HP {monster.Health.Current} should be < {hpBefore} after PowerShot");
    }

    [Fact]
    public void SkillOnCooldown_DoesNotExecute()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Health.Current = 50;

        // Use Heal once to set cooldown
        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 1;
        engine.Tick();

        int hpAfterFirstHeal = player.Health.Current;

        // Try to use Heal again while on cooldown
        // Damage a bit first
        player.Health.Current = 50;

        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 1;
        engine.Tick();

        Assert.Equal(50, player.Health.Current); // Should not have healed (on cooldown)
    }

    [Fact]
    public void InvalidSkillSlot_DoesNotCrash()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Use an invalid skill slot (99)
        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 99;
        engine.Tick(); // Should not crash
    }

    [Fact]
    public void Skill_NoTarget_MissesGracefully()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // PowerStrike with no monster at target - should not crash
        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        // Skill should not set cooldown when it misses
        Assert.Equal(0, player.Skills.Cooldown0);
    }

    [Fact]
    public void Cooldown_TicksAllSlots()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Manually set cooldowns on all 4 player.Skills
        player.Skills.Cooldown0 = 5;
        player.Skills.Cooldown1 = 5;
        player.Skills.Cooldown2 = 5;
        player.Skills.Cooldown3 = 5;

        engine.Tick();

        Assert.Equal(4, player.Skills.Cooldown0);
        Assert.Equal(4, player.Skills.Cooldown1);
        Assert.Equal(4, player.Skills.Cooldown2);
        Assert.Equal(4, player.Skills.Cooldown3);
    }

    [Fact]
    public void Skill_Slot2And3_Work()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Manually assign skills to player.Skills 2 and 3
        player.Skills.Skill2 = SkillDefinitions.Heal;
        player.Skills.Skill3 = SkillDefinitions.Dodge;

        // Damage player
        player.Health.Current = 50;

        // Use skill slot 2 (Heal)
        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 2;
        engine.Tick();

        Assert.True(player.Health.Current > 50, "Skill in slot 2 should have healed");

        Assert.True(player.Skills.Cooldown2 > 0, "Cooldown2 should be set");
    }

    [Fact]
    public void Skill_Slot3_Dodge()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Assign Dodge to slot 3
        player.Skills.Skill3 = SkillDefinitions.Dodge;

        int defBefore = player.CombatStats.Defense;

        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 3;
        engine.Tick();

        Assert.True(player.CombatStats.Defense > defBefore, "Dodge in slot 3 should boost defense");

        Assert.True(player.Skills.Cooldown3 > 0, "Cooldown3 should be set after Dodge");
    }

    [Fact]
    public void PowerShot_OutOfRange_DoesNotDamage()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Spawn monster well beyond PowerShot range (range=5)
        var _m = engine.SpawnMonster(Position.FromCoords(sx + 10, sy, Position.DefaultZ), new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(_m.Id);

        int hpBefore = monster.Health.Current;

        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 10;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.Equal(hpBefore, monster.Health.Current);
    }

    [Fact]
    public void Trap_IsNotImplemented_DoesNotSetCooldown()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Assign Trap to slot 1
        player.Skills.Skill1 = SkillDefinitions.Trap;

        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 1;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.Equal(0, player.Skills.Cooldown1); // Trap returns false, no cooldown
    }

    [Fact]
    public void Fireball_NoEnemiesInArea_NoCooldown()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // No monsters nearby
        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 3;
        player.Input.TargetY = 0;
        engine.Tick();

        // Fireball missed (no enemies) → no cooldown set
        Assert.Equal(0, player.Skills.Cooldown0);
    }

    [Fact]
    public void PowerShot_OutOfRange_NoCooldown()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        player.Input.ActionType = ActionTypes.UseSkill;
        player.Input.ItemSlot = 0;
        player.Input.TargetX = 10; // Beyond range 5
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.Equal(0, player.Skills.Cooldown0);
    }
}
