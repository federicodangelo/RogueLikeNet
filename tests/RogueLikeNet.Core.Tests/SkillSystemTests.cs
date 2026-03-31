using Arch.Core;
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
        engine.EnsureChunkLoaded(0, 0, Position.DefaultZ);
        return engine;
    }

    [Fact]
    public void Heal_RestoresHealth()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Mage);

        // Damage the player
        ref var health = ref engine.EcsWorld.Get<Health>(player);
        health.Current = 50;

        // Use Heal skill (slot 1 for Mage)
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 1; // Heal is slot 1 for Mage
        engine.Tick();

        ref var healthAfter = ref engine.EcsWorld.Get<Health>(player);
        Assert.True(healthAfter.Current > 50, $"Health {healthAfter.Current} should be > 50 after Heal");
    }

    [Fact]
    public void Skill_SetsCooldown()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Mage);

        ref var health = ref engine.EcsWorld.Get<Health>(player);
        health.Current = 50;

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 1; // Heal
        engine.Tick();

        ref var slots = ref engine.EcsWorld.Get<SkillSlots>(player);
        Assert.True(slots.Cooldown1 > 0, "Cooldown should be set after using Heal skill");
    }

    [Fact]
    public void Cooldown_TicksDown()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Mage);

        ref var health = ref engine.EcsWorld.Get<Health>(player);
        health.Current = 50;

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 1;
        engine.Tick();

        ref var slots = ref engine.EcsWorld.Get<SkillSlots>(player);
        int cdAfterUse = slots.Cooldown1;

        engine.Tick(); // one more tick

        ref var slotsAfter = ref engine.EcsWorld.Get<SkillSlots>(player);
        Assert.True(slotsAfter.Cooldown1 < cdAfterUse, "Cooldown should decrease each tick");
    }

    [Fact]
    public void PowerStrike_DamagesMonster()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var mHealthBefore = ref engine.EcsWorld.Get<Health>(monster);
        int hpBefore = mHealthBefore.Current;

        // PowerStrike is slot 0 for Warrior, target direction (1, 0)
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 0;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();

        ref var mHealthAfter = ref engine.EcsWorld.Get<Health>(monster);
        Assert.True(mHealthAfter.Current < hpBefore, $"Monster HP {mHealthAfter.Current} should be < {hpBefore} after PowerStrike");
    }

    [Fact]
    public void ShieldBash_DamagesMonster()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var mHealthBefore = ref engine.EcsWorld.Get<Health>(monster);
        int hpBefore = mHealthBefore.Current;

        // ShieldBash is slot 1 for Warrior
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 1;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();

        ref var mHealthAfter = ref engine.EcsWorld.Get<Health>(monster);
        Assert.True(mHealthAfter.Current < hpBefore, $"Monster HP {mHealthAfter.Current} should be < {hpBefore} after ShieldBash");
    }

    [Fact]
    public void Backstab_DamagesMonster()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Rogue);
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var mHealthBefore = ref engine.EcsWorld.Get<Health>(monster);
        int hpBefore = mHealthBefore.Current;

        // Backstab is slot 0 for Rogue
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 0;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();

        ref var mHealthAfter = ref engine.EcsWorld.Get<Health>(monster);
        Assert.True(mHealthAfter.Current < hpBefore, $"Monster HP {mHealthAfter.Current} should be < {hpBefore} after Backstab");
    }

    [Fact]
    public void Dodge_BoostsDefense()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Rogue);

        ref var statsBefore = ref engine.EcsWorld.Get<CombatStats>(player);
        int defBefore = statsBefore.Defense;

        // Dodge is slot 1 for Rogue
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 1;
        engine.Tick();

        ref var statsAfter = ref engine.EcsWorld.Get<CombatStats>(player);
        Assert.True(statsAfter.Defense > defBefore, $"Defense {statsAfter.Defense} should be > {defBefore} after Dodge");
    }

    [Fact]
    public void Fireball_DamagesMonsterInArea()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Mage);
        // Spawn monster near the target area
        var monster = engine.SpawnMonster(sx + 1, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var mHealthBefore = ref engine.EcsWorld.Get<Health>(monster);
        int hpBefore = mHealthBefore.Current;

        // Fireball is slot 0 for Mage, target direction (1, 0)
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 0;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();

        ref var mHealthAfter = ref engine.EcsWorld.Get<Health>(monster);
        Assert.True(mHealthAfter.Current < hpBefore, $"Monster HP {mHealthAfter.Current} should be < {hpBefore} after Fireball");
    }

    [Fact]
    public void PowerShot_DamagesMonsterAtRange()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Ranger);
        // Spawn monster at distance
        var monster = engine.SpawnMonster(sx + 3, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var mHealthBefore = ref engine.EcsWorld.Get<Health>(monster);
        int hpBefore = mHealthBefore.Current;

        // PowerShot is slot 0 for Ranger, target direction (3, 0)
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 0;
        input.TargetX = 3;
        input.TargetY = 0;
        engine.Tick();

        ref var mHealthAfter = ref engine.EcsWorld.Get<Health>(monster);
        Assert.True(mHealthAfter.Current < hpBefore, $"Monster HP {mHealthAfter.Current} should be < {hpBefore} after PowerShot");
    }

    [Fact]
    public void SkillOnCooldown_DoesNotExecute()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Mage);

        ref var health = ref engine.EcsWorld.Get<Health>(player);
        health.Current = 50;

        // Use Heal once to set cooldown
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 1;
        engine.Tick();

        int hpAfterFirstHeal = engine.EcsWorld.Get<Health>(player).Current;

        // Try to use Heal again while on cooldown
        // Damage a bit first
        ref var health2 = ref engine.EcsWorld.Get<Health>(player);
        health2.Current = 50;

        ref var input2 = ref engine.EcsWorld.Get<PlayerInput>(player);
        input2.ActionType = ActionTypes.UseSkill;
        input2.ItemSlot = 1;
        engine.Tick();

        ref var healthAfter = ref engine.EcsWorld.Get<Health>(player);
        Assert.Equal(50, healthAfter.Current); // Should not have healed (on cooldown)
    }

    [Fact]
    public void InvalidSkillSlot_DoesNotCrash()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        // Use an invalid skill slot (99)
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 99;
        engine.Tick(); // Should not crash
    }

    [Fact]
    public void Skill_NoTarget_MissesGracefully()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        // PowerStrike with no monster at target - should not crash
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 0;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();

        // Skill should not set cooldown when it misses
        ref var slots = ref engine.EcsWorld.Get<SkillSlots>(player);
        Assert.Equal(0, slots.Cooldown0);
    }

    [Fact]
    public void Cooldown_TicksAllSlots()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        // Manually set cooldowns on all 4 slots
        ref var slots = ref engine.EcsWorld.Get<SkillSlots>(player);
        slots.Cooldown0 = 5;
        slots.Cooldown1 = 5;
        slots.Cooldown2 = 5;
        slots.Cooldown3 = 5;

        engine.Tick();

        ref var slotsAfter = ref engine.EcsWorld.Get<SkillSlots>(player);
        Assert.Equal(4, slotsAfter.Cooldown0);
        Assert.Equal(4, slotsAfter.Cooldown1);
        Assert.Equal(4, slotsAfter.Cooldown2);
        Assert.Equal(4, slotsAfter.Cooldown3);
    }

    [Fact]
    public void Skill_Slot2And3_Work()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        // Manually assign skills to slots 2 and 3
        ref var slots = ref engine.EcsWorld.Get<SkillSlots>(player);
        slots.Skill2 = SkillDefinitions.Heal;
        slots.Skill3 = SkillDefinitions.Dodge;

        // Damage player
        ref var health = ref engine.EcsWorld.Get<Health>(player);
        health.Current = 50;

        // Use skill slot 2 (Heal)
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 2;
        engine.Tick();

        ref var healthAfter = ref engine.EcsWorld.Get<Health>(player);
        Assert.True(healthAfter.Current > 50, "Skill in slot 2 should have healed");

        ref var slotsAfter = ref engine.EcsWorld.Get<SkillSlots>(player);
        Assert.True(slotsAfter.Cooldown2 > 0, "Cooldown2 should be set");
    }

    [Fact]
    public void Skill_Slot3_Dodge()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Warrior);

        // Assign Dodge to slot 3
        ref var slots = ref engine.EcsWorld.Get<SkillSlots>(player);
        slots.Skill3 = SkillDefinitions.Dodge;

        ref var statsBefore = ref engine.EcsWorld.Get<CombatStats>(player);
        int defBefore = statsBefore.Defense;

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 3;
        engine.Tick();

        ref var statsAfter = ref engine.EcsWorld.Get<CombatStats>(player);
        Assert.True(statsAfter.Defense > defBefore, "Dodge in slot 3 should boost defense");

        ref var slotsAfter = ref engine.EcsWorld.Get<SkillSlots>(player);
        Assert.True(slotsAfter.Cooldown3 > 0, "Cooldown3 should be set after Dodge");
    }

    [Fact]
    public void PowerShot_OutOfRange_DoesNotDamage()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Ranger);
        // Spawn monster well beyond PowerShot range (range=5)
        var monster = engine.SpawnMonster(sx + 10, sy, Position.DefaultZ, new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });

        ref var mHealthBefore = ref engine.EcsWorld.Get<Health>(monster);
        int hpBefore = mHealthBefore.Current;

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 0;
        input.TargetX = 10;
        input.TargetY = 0;
        engine.Tick();

        ref var mHealthAfter = ref engine.EcsWorld.Get<Health>(monster);
        Assert.Equal(hpBefore, mHealthAfter.Current);
    }

    [Fact]
    public void Trap_IsNotImplemented_DoesNotSetCooldown()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Ranger);

        // Assign Trap to slot 1
        ref var slots = ref engine.EcsWorld.Get<SkillSlots>(player);
        slots.Skill1 = SkillDefinitions.Trap;

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 1;
        input.TargetX = 1;
        input.TargetY = 0;
        engine.Tick();

        ref var slotsAfter = ref engine.EcsWorld.Get<SkillSlots>(player);
        Assert.Equal(0, slotsAfter.Cooldown1); // Trap returns false, no cooldown
    }

    [Fact]
    public void Fireball_NoEnemiesInArea_NoCooldown()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Mage);

        // No monsters nearby
        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 0;
        input.TargetX = 3;
        input.TargetY = 0;
        engine.Tick();

        ref var slots = ref engine.EcsWorld.Get<SkillSlots>(player);
        // Fireball missed (no enemies) → no cooldown set
        Assert.Equal(0, slots.Cooldown0);
    }

    [Fact]
    public void PowerShot_OutOfRange_NoCooldown()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, Position.DefaultZ, ClassDefinitions.Ranger);

        ref var input = ref engine.EcsWorld.Get<PlayerInput>(player);
        input.ActionType = ActionTypes.UseSkill;
        input.ItemSlot = 0;
        input.TargetX = 10; // Beyond range 5
        input.TargetY = 0;
        engine.Tick();

        ref var slots = ref engine.EcsWorld.Get<SkillSlots>(player);
        Assert.Equal(0, slots.Cooldown0);
    }
}
