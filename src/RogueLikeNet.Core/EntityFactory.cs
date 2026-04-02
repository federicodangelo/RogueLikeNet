using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Core;

/// <summary>
/// Creates ECS entities with their canonical component sets.
/// Centralises entity construction so both game logic (GameEngine) and
/// persistence (EntitySerializer) use the same templates.
/// </summary>
public static class EntityFactory
{
    /// <summary>Creates a monster entity at the given world position.</summary>
    public static Entity CreateMonster(Arch.Core.World world, int x, int y, int z, MonsterData data)
    {
        var def = NpcDefinitions.Get(data.MonsterTypeId);
        int moveInterval = Math.Max(0, 10 - data.Speed);
        return world.Create(
            new Position(x, y, z),
            new Health(data.Health),
            new CombatStats(data.Attack, data.Defense, data.Speed),
            new TileAppearance(def.GlyphId, def.Color),
            data,
            new AIState { StateId = AIStates.Idle },
            new MoveDelay(moveInterval),
            new AttackDelay(moveInterval)
        );
    }

    /// <summary>Creates an item entity lying on the ground.</summary>
    public static Entity CreateItemOnGround(Arch.Core.World world, ItemData itemData, int x, int y, int z)
    {
        var def = ItemDefinitions.Get(itemData.ItemTypeId);
        return world.Create(
            new Position(x, y, z),
            new TileAppearance(def.GlyphId, def.Color),
            itemData
        );
    }

    /// <summary>Creates a dungeon element (decoration with optional light).</summary>
    public static Entity CreateElement(Arch.Core.World world, Position pos, TileAppearance appearance, LightSource? light = null)
    {
        if (light.HasValue)
            return world.Create(pos, appearance, light.Value);
        return world.Create(pos, appearance);
    }

    /// <summary>Creates a resource node entity from its definition.</summary>
    public static Entity CreateResourceNode(Arch.Core.World world, int x, int y, int z, ResourceNodeDefinition def)
    {
        return world.Create(
            new Position(x, y, z),
            new Health(def.Health),
            new CombatStats(0, def.Defense, 0),
            new TileAppearance(def.GlyphId, def.Color),
            new ResourceNodeData
            {
                NodeTypeId = def.NodeTypeId,
                ResourceItemTypeId = def.ResourceItemTypeId,
                MinDrop = def.MinDrop,
                MaxDrop = def.MaxDrop,
            },
            new AttackDelay(0)
        );
    }

    /// <summary>Creates a peaceful town NPC entity.</summary>
    public static Entity CreateTownNpc(Arch.Core.World world, int x, int y, int z, string name, int townCenterX, int townCenterY, int wanderRadius)
    {
        return world.Create(
            new Position(x, y, z),
            new Health(9999),
            new CombatStats(0, 999, 3),
            new TileAppearance(TileDefinitions.GlyphTownNpc, TileDefinitions.ColorTownNpcFg),
            new AIState { StateId = AIStates.Idle },
            new MoveDelay(5),
            new AttackDelay(0),
            new TownNpcTag
            {
                Name = name,
                TownCenterX = townCenterX,
                TownCenterY = townCenterY,
                WanderRadius = wanderRadius,
                TalkTimer = 0,
                DialogueIndex = 0,
            }
        );
    }

    /// <summary>Creates a player entity. Class choice affects starting stats.</summary>
    public static Entity CreatePlayer(Arch.Core.World world, long connectionId, int x, int y, int z, int classId)
    {
        var def = ClassDefinitions.Get(classId);
        var classStats = def.StartingStats;
        var stats = classStats + ClassDefinitions.BaseStats;

        var moveDelay = Math.Max(0, 10 - (6 + classStats.Speed));
        var attackDelay = Math.Max(0, 10 - (6 + classStats.Speed));

        return world.Create(
            new Position(x, y, z),
            new Health(stats.Health),
            new CombatStats(stats.Attack, stats.Defense, stats.Speed),
            new FOVData(ClassDefinitions.FOVRadius),
            new TileAppearance(TileDefinitions.GlyphPlayer, TileDefinitions.ColorWhite),
            new PlayerTag { ConnectionId = connectionId },
            new PlayerInput(),
            new ClassData { ClassId = classId, Level = 1 },
            new SkillSlots { Skill0 = def.StartingSkill0, Skill1 = def.StartingSkill1 },
            new Inventory(ClassDefinitions.InventorySlots),
            new Equipment(),
            new QuickSlots(),
            new MoveDelay(moveDelay),
            new AttackDelay(attackDelay)
        );
    }
}
