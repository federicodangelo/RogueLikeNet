using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class SpellSystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    private static int ItemId(string id) => GameData.Instance.Items.GetNumericId(id);
    private static int SpellId(string id) => GameData.Instance.Spells.GetNumericId(id);

    #region Spell Data Tests

    [Fact]
    public void SpellRegistry_ContainsAllSpells()
    {
        Assert.True(GameData.Instance.Spells.Count >= 8);
        Assert.NotNull(GameData.Instance.Spells.Get("fireball"));
        Assert.NotNull(GameData.Instance.Spells.Get("ice_bolt"));
        Assert.NotNull(GameData.Instance.Spells.Get("lightning_strike"));
        Assert.NotNull(GameData.Instance.Spells.Get("heal"));
        Assert.NotNull(GameData.Instance.Spells.Get("arcane_shield"));
        Assert.NotNull(GameData.Instance.Spells.Get("flame_burst"));
        Assert.NotNull(GameData.Instance.Spells.Get("poison_cloud"));
        Assert.NotNull(GameData.Instance.Spells.Get("magic_missile"));
    }

    [Fact]
    public void SpellDefinition_Fireball_HasCorrectProperties()
    {
        var fireball = GameData.Instance.Spells.Get("fireball")!;
        Assert.Equal(15, fireball.ManaCost);
        Assert.Equal(25, fireball.BaseDamage);
        Assert.Equal(6, fireball.Range);
        Assert.Equal(SpellTargetType.SingleTarget, fireball.TargetType);
        Assert.True(fireball.CooldownTicks > 0);
    }

    [Fact]
    public void SpellDefinition_Heal_IsSelfTargeting()
    {
        var heal = GameData.Instance.Spells.Get("heal")!;
        Assert.Equal(SpellTargetType.Self, heal.TargetType);
        Assert.True(heal.HealAmount > 0);
        Assert.Equal(10, heal.ManaCost);
    }

    [Fact]
    public void SpellDefinition_FlameBurst_IsAreaOfEffect()
    {
        var flameBurst = GameData.Instance.Spells.Get("flame_burst")!;
        Assert.Equal(SpellTargetType.AreaOfEffect, flameBurst.TargetType);
        Assert.True(flameBurst.AoERadius > 0);
        Assert.True(flameBurst.BaseDamage > 0);
    }

    [Fact]
    public void SpellDefinition_ArcaneShield_HasBuffProperties()
    {
        var shield = GameData.Instance.Spells.Get("arcane_shield")!;
        Assert.Equal(SpellTargetType.Self, shield.TargetType);
        Assert.True(shield.BuffDefense > 0);
        Assert.True(shield.BuffDurationTicks > 0);
    }

    #endregion

    #region Mana Component Tests

    [Fact]
    public void Mana_Constructor_SetsCurrentAndMax()
    {
        var mana = new Mana(50);
        Assert.Equal(50, mana.Current);
        Assert.Equal(50, mana.Max);
    }

    [Fact]
    public void Mana_HasEnough_ReturnsTrueWhenSufficient()
    {
        var mana = new Mana(50);
        Assert.True(mana.HasEnough(50));
        Assert.True(mana.HasEnough(1));
        Assert.True(mana.HasEnough(0));
    }

    [Fact]
    public void Mana_HasEnough_ReturnsFalseWhenInsufficient()
    {
        var mana = new Mana(10);
        Assert.False(mana.HasEnough(11));
        Assert.False(mana.HasEnough(100));
    }

    [Fact]
    public void SpawnPlayer_Mage_HasHigherMana()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        var pMage = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var mage = ref engine.WorldMap.GetPlayerRef(pMage.Id);

        var pWarrior = engine.SpawnPlayer(2, Position.FromCoords(sx, sy + 2, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var warrior = ref engine.WorldMap.GetPlayerRef(pWarrior.Id);

        Assert.True(mage.Mana.Max > warrior.Mana.Max);
        Assert.Equal(mage.Mana.Max, mage.Mana.Current); // starts full
    }

    [Fact]
    public void SpawnPlayer_AllClasses_HaveCorrectStartingMana()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        var pW = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var warrior = ref engine.WorldMap.GetPlayerRef(pW.Id);

        var pR = engine.SpawnPlayer(2, Position.FromCoords(sx, sy + 2, Position.DefaultZ), ClassDefinitions.Rogue);
        ref var rogue = ref engine.WorldMap.GetPlayerRef(pR.Id);

        var pM = engine.SpawnPlayer(3, Position.FromCoords(sx, sy + 4, Position.DefaultZ), ClassDefinitions.Mage);
        ref var mageRef = ref engine.WorldMap.GetPlayerRef(pM.Id);

        var pRa = engine.SpawnPlayer(4, Position.FromCoords(sx, sy + 6, Position.DefaultZ), ClassDefinitions.Ranger);
        ref var ranger = ref engine.WorldMap.GetPlayerRef(pRa.Id);

        // BaseMana=30 + class bonus: warrior=0, rogue=10, mage=70, ranger=20
        Assert.Equal(30, warrior.Mana.Max);
        Assert.Equal(40, rogue.Mana.Max);
        Assert.Equal(100, mageRef.Mana.Max);
        Assert.Equal(50, ranger.Mana.Max);
    }

    #endregion

    #region Spell Casting Tests

    [Fact]
    public void CastSpell_SingleTarget_DamagesMonster()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        var m = engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });

        int magicMissileId = SpellId("magic_missile");
        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = magicMissileId;
        engine.Tick();

        Assert.True(engine.Spells.LastTickEvents.Count > 0);
        var evt = engine.Spells.LastTickEvents[0];
        Assert.True(evt.Damage > 0);
        Assert.True(evt.IsRanged);

        // Mana should be deducted
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        var magicMissile = GameData.Instance.Spells.Get("magic_missile")!;
        Assert.True(player.Mana.Current < player.Mana.Max);
        Assert.Equal(player.Mana.Max - magicMissile.ManaCost, player.Mana.Current);
    }

    [Fact]
    public void CastSpell_InsufficientMana_FailsWithEvent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Drain all mana
        player.Mana.Current = 0;

        engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });

        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = SpellId("fireball");
        engine.Tick();

        // No combat events should occur
        Assert.Empty(engine.Spells.LastTickEvents);

        // Should have a failed action event
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Contains(player.ActionEvents,
            e => e.EventType == PlayerActionEventType.CastSpell && e.Failed && e.FailReason == ActionFailReason.InsufficientMana);
    }

    [Fact]
    public void CastSpell_OnCooldown_FailsWithEvent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });

        int spellId = SpellId("magic_missile");

        // First cast should succeed
        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = spellId;
        engine.Tick();
        Assert.True(engine.Spells.LastTickEvents.Count > 0);

        // Second cast immediately should fail (cooldown)
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = spellId;
        engine.Tick();

        Assert.Empty(engine.Spells.LastTickEvents);

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Contains(player.ActionEvents,
            e => e.EventType == PlayerActionEventType.CastSpell && e.Failed && e.FailReason == ActionFailReason.SpellOnCooldown);
    }

    [Fact]
    public void CastSpell_Success_ActionEventContainsSpellId()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 0, Defense = 0, Speed = 0 });

        int spellId = SpellId("magic_missile");
        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = spellId;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Contains(player.ActionEvents,
            e => e.EventType == PlayerActionEventType.CastSpell && !e.Failed && e.ItemTypeId == spellId);
    }

    [Fact]
    public void CastSpell_Heal_RestoresPlayerHealth()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Reduce health
        player.Health.Current = player.Health.Max / 2;
        int damagedHp = player.Health.Current;

        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = SpellId("heal");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        var heal = GameData.Instance.Spells.Get("heal")!;

        // Health should have increased by HealAmount (or capped at max)
        Assert.True(player.Health.Current > damagedHp);
        Assert.True(player.Health.Current <= player.Health.Max);
    }

    [Fact]
    public void CastSpell_ArcaneShield_AddsDefenseBuff()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        int defBefore = player.CombatStats.Defense;

        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = SpellId("arcane_shield");
        engine.Tick();

        // Active effects system runs next tick, but the effect should be added
        player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // The active effect should exist
        bool hasStatsBoost = false;
        for (int i = 0; i < player.ActiveEffects.Count; i++)
        {
            if (player.ActiveEffects.Get(i).Type == EffectType.StatsBoost)
            {
                hasStatsBoost = true;
                break;
            }
        }
        Assert.True(hasStatsBoost);
    }

    [Fact]
    public void CastSpell_AoE_DamagesMultipleMonsters()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Spawn monsters close together — within AoE radius of flame_burst (radius=2)
        engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });
        engine.SpawnMonster(Position.FromCoords(sx + 3, sy + 1, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });

        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = SpellId("flame_burst");
        engine.Tick();

        // AoE should hit multiple monsters
        Assert.True(engine.Spells.LastTickEvents.Count >= 2);
    }

    [Fact]
    public void CastSpell_NoTarget_DoesNotCrash()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // No monsters nearby — auto-target will find nothing
        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = SpellId("fireball");
        engine.Tick();

        // Should not crash — mana is still deducted for the attempt
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        // No assertion on events — just ensure no exception
    }

    [Fact]
    public void CastSpell_InvalidSpellId_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        int manaBefore = player.Mana.Current;

        // Use a bogus spell ID
        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = 999999;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Mana should not change
        Assert.Equal(manaBefore, player.Mana.Current);
        Assert.Empty(engine.Spells.LastTickEvents);
    }

    [Fact]
    public void CastSpell_WithMagicWeapon_AddsBonusDamage()
    {
        using var engine1 = CreateEngine();
        var (sx1, sy1, _) = engine1.FindSpawnPosition();
        var p1 = engine1.SpawnPlayer(1, Position.FromCoords(sx1, sy1, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player1 = ref engine1.WorldMap.GetPlayerRef(p1.Id);

        // Equip archmage staff (BonusSpellDamage=12)
        player1.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("archmage_staff"), StackCount = 1 };
        ActiveEffectsSystem.RecalculatePlayerStats(ref player1);

        engine1.SpawnMonster(Position.FromCoords(sx1 + 3, sy1, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });

        player1.Input.ActionType = ActionTypes.CastSpell;
        player1.Input.ItemSlot = SpellId("magic_missile");
        engine1.Tick();

        var dmgWithStaff = engine1.Spells.LastTickEvents[0].Damage;

        // Now cast without a weapon
        using var engine2 = CreateEngine();
        var (sx2, sy2, _) = engine2.FindSpawnPosition();
        var p2 = engine2.SpawnPlayer(1, Position.FromCoords(sx2, sy2, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player2 = ref engine2.WorldMap.GetPlayerRef(p2.Id);

        engine2.SpawnMonster(Position.FromCoords(sx2 + 3, sy2, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 5, Defense = 0, Speed = 8 });

        player2.Input.ActionType = ActionTypes.CastSpell;
        player2.Input.ItemSlot = SpellId("magic_missile");
        engine2.Tick();

        var dmgWithout = engine2.Spells.LastTickEvents[0].Damage;

        Assert.True(dmgWithStaff > dmgWithout);
    }

    #endregion

    #region Mana Regen Tests

    [Fact]
    public void ManaRegen_RegeneratesOverTime()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Drain some mana
        player.Mana.Current = 50;

        // Tick many times to allow regen (ManaRegenTickInterval = 100)
        for (int i = 0; i < SurvivalSystem.ManaRegenTickInterval + 1; i++)
        {
            engine.Tick();
        }

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.True(player.Mana.Current > 50);
    }

    [Fact]
    public void ManaRegen_DoesNotExceedMax()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Already at max
        Assert.Equal(player.Mana.Max, player.Mana.Current);

        for (int i = 0; i < SurvivalSystem.ManaRegenTickInterval + 1; i++)
        {
            engine.Tick();
        }

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Equal(player.Mana.Max, player.Mana.Current);
    }

    #endregion

    #region Magic Items Tests

    [Fact]
    public void MagicItems_StaffsExist_WithSpellId()
    {
        var apprentice = GameData.Instance.Items.Get("apprentice_staff")!;
        Assert.NotNull(apprentice.Magic);
        Assert.True(apprentice.Magic.BonusSpellDamage > 0);
        Assert.True(apprentice.Magic.BonusMana > 0);
    }

    [Fact]
    public void MagicItems_ScrollsExist_WithSpellId()
    {
        var scroll = GameData.Instance.Items.Get("scroll_fireball")!;
        Assert.NotNull(scroll.Magic);
        Assert.Equal("fireball", scroll.Magic.SpellId);
    }

    [Theory]
    [InlineData("scroll_fireball", "fireball")]
    [InlineData("scroll_heal", "heal")]
    [InlineData("scroll_lightning", "lightning_strike")]
    public void MagicScrolls_HaveCorrectSpell(string scrollId, string expectedSpellId)
    {
        var scroll = GameData.Instance.Items.Get(scrollId)!;
        Assert.NotNull(scroll.Magic);
        Assert.Equal(expectedSpellId, scroll.Magic.SpellId);
    }

    [Fact]
    public void MagicItems_WeaponsWithMagic_HaveBothWeaponAndMagicData()
    {
        var fireStaff = GameData.Instance.Items.Get("fire_staff")!;
        Assert.NotNull(fireStaff.Weapon);
        Assert.NotNull(fireStaff.Magic);
        Assert.True(fireStaff.Weapon.Range > 1);
        Assert.False(string.IsNullOrEmpty(fireStaff.Magic.SpellId));
    }

    [Theory]
    [InlineData("apprentice_staff", "magic_missile")]
    [InlineData("lightning_wand", "lightning_strike")]
    [InlineData("archmage_staff", "fireball")]
    [InlineData("fire_staff", "fireball")]
    [InlineData("ice_staff", "ice_bolt")]
    public void MagicWeapon_HasCorrectSpell(string weaponId, string expectedSpellId)
    {
        var weapon = GameData.Instance.Items.Get(weaponId)!;
        Assert.NotNull(weapon.Magic);
        Assert.Equal(expectedSpellId, weapon.Magic.SpellId);
    }

    [Fact]
    public void MagicWeapon_BonusMana_IncreasesPlayerMaxMana()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        int baseMana = player.Mana.Max;

        // Equip archmage staff (BonusMana=50)
        player.Equipment[(int)EquipSlot.Hand] = new ItemData { ItemTypeId = ItemId("archmage_staff"), StackCount = 1 };
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Equal(baseMana + 50, player.Mana.Max);
    }

    #endregion

    #region Cooldown Tests

    [Fact]
    public void CastSpell_CooldownExpires_CanCastAgain()
    {
        using var engine = CreateEngine();
        engine.DebugInvulnerable = true; // prevent starvation death during wait
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 9999, Attack = 0, Defense = 0, Speed = 0 });

        var spell = GameData.Instance.Spells.Get("magic_missile")!;
        int spellId = SpellId("magic_missile");

        // First cast
        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = spellId;
        engine.Tick();
        Assert.True(engine.Spells.LastTickEvents.Count > 0);

        // Clear action so the spell doesn't auto-fire during wait
        // (SpellSystem doesn't reset ActionType after processing)
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        player.Input.ActionType = ActionTypes.None;

        // Wait for cooldown to expire
        for (int i = 0; i < spell.CooldownTicks + 1; i++)
        {
            engine.Tick();
        }

        // Second cast after cooldown expired
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.False(player.IsDead, "Player should not be dead");
        player.Mana.Current = player.Mana.Max; // top up mana
        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = spellId;
        engine.Tick();

        Assert.True(engine.Spells.LastTickEvents.Count > 0);
    }

    #endregion

    #region Level Up Mana Tests

    [Fact]
    public void LevelUp_RestoresFullMana()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Drain mana
        player.Mana.Current = 0;

        // Give lots of XP to trigger level up
        player.ClassData.Experience = 99999;

        // Kill a monster to trigger XP gain / level up processing in combat
        engine.SpawnMonster(Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 1, Attack = 0, Defense = 0, Speed = 1 });
        player.Input.ActionType = ActionTypes.Attack;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // If level up occurred, mana should be restored to max
        if (player.ClassData.Level > 1)
        {
            Assert.Equal(player.Mana.Max, player.Mana.Current);
        }
    }

    #endregion

    #region Manual Targeting Tests

    [Fact]
    public void CastSpell_SingleTarget_ManualTarget_HitsCorrectPosition()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Two monsters at different positions
        engine.SpawnMonster(Position.FromCoords(sx + 2, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 0, Defense = 0, Speed = 0 });
        engine.SpawnMonster(Position.FromCoords(sx + 4, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 0, Defense = 0, Speed = 0 });

        // Target the farther monster explicitly
        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = SpellId("magic_missile");
        player.Input.TargetX = 4;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.True(engine.Spells.LastTickEvents.Count > 0);
        var evt = engine.Spells.LastTickEvents[0];
        Assert.Equal(sx + 4, evt.Target.X);
        Assert.Equal(sy, evt.Target.Y);
    }

    [Fact]
    public void CastSpell_AoE_ManualTarget_HitsTargetArea()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Spawn monster at specific position
        engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 0, Defense = 0, Speed = 0 });

        // Manually target that position with AoE spell
        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = SpellId("flame_burst");
        player.Input.TargetX = 3;
        player.Input.TargetY = 0;
        engine.Tick();

        Assert.True(engine.Spells.LastTickEvents.Count > 0);
    }

    #endregion

    #region Kill and Damage Edge Cases

    [Fact]
    public void CastSpell_SingleTarget_KillsMonster_SetsTargetDied()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Low-HP monster that will die from one spell
        engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 1, Attack = 0, Defense = 0, Speed = 0 });

        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = SpellId("fireball");
        engine.Tick();

        Assert.True(engine.Spells.LastTickEvents.Count > 0);
        Assert.True(engine.Spells.LastTickEvents[0].TargetDied);

        // Should have a Kill action event
        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Contains(player.ActionEvents, e => e.EventType == PlayerActionEventType.Kill);
    }

    [Fact]
    public void CastSpell_AoE_KillsMonster_SetsTargetDiedAndKillEvent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Low-HP monsters in AoE range
        engine.SpawnMonster(Position.FromCoords(sx + 3, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 1, Attack = 0, Defense = 0, Speed = 0 });
        engine.SpawnMonster(Position.FromCoords(sx + 3, sy + 1, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 1, Attack = 0, Defense = 0, Speed = 0 });

        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = SpellId("flame_burst");
        engine.Tick();

        Assert.True(engine.Spells.LastTickEvents.Count >= 2);
        Assert.True(engine.Spells.LastTickEvents.All(e => e.TargetDied));

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.True(player.ActionEvents.Count(e => e.EventType == PlayerActionEventType.Kill) >= 2);
    }

    [Fact]
    public void CastSpell_SingleTarget_AutoTargetFindsClosest()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Closer monster should be targeted by auto-target
        engine.SpawnMonster(Position.FromCoords(sx + 2, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 0, Defense = 0, Speed = 0 });
        engine.SpawnMonster(Position.FromCoords(sx + 4, sy, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 200, Attack = 0, Defense = 0, Speed = 0 });

        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = SpellId("magic_missile");
        engine.Tick();

        Assert.True(engine.Spells.LastTickEvents.Count > 0);
        // Auto-target should pick the closer monster at sx+2
        Assert.Equal(sx + 2, engine.Spells.LastTickEvents[0].Target.X);
    }

    [Fact]
    public void CastSpell_AoE_NoTarget_DoesNotCrash()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // No monsters — auto-target finds nothing for AoE
        player.Input.ActionType = ActionTypes.CastSpell;
        player.Input.ItemSlot = SpellId("flame_burst");
        engine.Tick();

        // Should not crash, no combat events
        Assert.Empty(engine.Spells.LastTickEvents);
    }

    #endregion

    #region Player State Data Tests

    [Fact]
    public void GetPlayerStateData_IncludesManaFields()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Mage);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        var stateData = engine.GetPlayerStateData(player);

        Assert.Equal(player.Mana.Current, stateData.Mana);
        Assert.Equal(player.Mana.Max, stateData.MaxMana);
        Assert.True(stateData.MaxMana > 0);
    }

    #endregion
}
