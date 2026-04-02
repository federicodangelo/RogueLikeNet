using Arch.Core;
using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core;

/// <summary>
/// Centralized ECS query descriptions used throughout the game.
/// Reuse these instead of creating new <see cref="QueryDescription"/> instances each time.
/// </summary>
public static class GameQueries
{
    // ── Position & Actor ─────────────────────────────────────────────

    public static readonly QueryDescription AllPositioned =
        new QueryDescription().WithAll<Position>();

    public static readonly QueryDescription NonPlayerPositioned =
        new QueryDescription().WithAll<Position>().WithNone<PlayerTag>();

    public static readonly QueryDescription PlayerPositions =
        new QueryDescription().WithAll<Position, PlayerTag>();

    public static readonly QueryDescription PositionedActors =
        new QueryDescription().WithAll<Position, Health>();

    public static readonly QueryDescription MonsterPositions =
        new QueryDescription().WithAll<Position, MonsterData>();

    public static readonly QueryDescription VelocityEntities =
        new QueryDescription().WithAll<Position, GridVelocity>();

    // ── Delays ───────────────────────────────────────────────────────

    public static readonly QueryDescription MoveDelays =
        new QueryDescription().WithAll<MoveDelay>();

    public static readonly QueryDescription AttackDelays =
        new QueryDescription().WithAll<AttackDelay>();

    // ── AI ────────────────────────────────────────────────────────────

    public static readonly QueryDescription MonsterAI =
        new QueryDescription().WithAll<Position, AIState, CombatStats, Health>().WithNone<DeadTag, TownNpcTag>();

    public static readonly QueryDescription AliveNpcs =
        new QueryDescription().WithAll<Position, TownNpcTag, MoveDelay, Health>().WithNone<DeadTag>();

    // ── Combat ───────────────────────────────────────────────────────

    public static readonly QueryDescription PlayerAttack =
        new QueryDescription().WithAll<Position, PlayerInput, CombatStats, AttackDelay>();

    public static readonly QueryDescription CombatTargets =
        new QueryDescription().WithAll<Position, Health, CombatStats>();

    public static readonly QueryDescription NonNpcCombatTargets =
        new QueryDescription().WithAll<Position, Health, CombatStats>().WithNone<TownNpcTag>();

    public static readonly QueryDescription AliveCombatTargets =
        new QueryDescription().WithAll<Position, Health, CombatStats>().WithNone<DeadTag>();

    public static readonly QueryDescription AliveEnemyCombatTargets =
        new QueryDescription().WithAll<Position, Health, CombatStats>().WithNone<DeadTag, PlayerTag>();

    public static readonly QueryDescription AliveEntities =
        new QueryDescription().WithAll<Health>().WithNone<DeadTag>();

    public static readonly QueryDescription PlayersWithCombat =
        new QueryDescription().WithAll<Position, Health, CombatStats, PlayerTag>();

    public static readonly QueryDescription MonsterAttackers =
        new QueryDescription().WithAll<Position, AIState, CombatStats, Health, AttackDelay>().WithNone<DeadTag, TownNpcTag>();

    // ── Movement ─────────────────────────────────────────────────────

    public static readonly QueryDescription PlayerMovement =
        new QueryDescription().WithAll<Position, PlayerInput, MoveDelay>();

    // ── Inventory & Equipment ────────────────────────────────────────

    public static readonly QueryDescription PlayerInventoryPosition =
        new QueryDescription().WithAll<PlayerInput, Inventory, Position>();

    public static readonly QueryDescription PlayerInventory =
        new QueryDescription().WithAll<PlayerInput, Inventory>();

    public static readonly QueryDescription PlayerInventoryHealth =
        new QueryDescription().WithAll<PlayerInput, Inventory, Health, CombatStats>();

    public static readonly QueryDescription PlayerEquipment =
        new QueryDescription().WithAll<PlayerInput, Inventory, Equipment, CombatStats>();

    public static readonly QueryDescription PlayerEquipmentFull =
        new QueryDescription().WithAll<PlayerInput, Inventory, Equipment, Health, CombatStats>();

    public static readonly QueryDescription PlayerQuickSlots =
        new QueryDescription().WithAll<PlayerInput, Inventory, QuickSlots>();

    public static readonly QueryDescription PlayerQuickSlotUse =
        new QueryDescription().WithAll<PlayerInput, Inventory, QuickSlots, Health, CombatStats>();

    // ── Skills ───────────────────────────────────────────────────────

    public static readonly QueryDescription SkillCooldowns =
        new QueryDescription().WithAll<SkillSlots>();

    public static readonly QueryDescription PlayerSkillUse =
        new QueryDescription().WithAll<PlayerInput, SkillSlots, Position, CombatStats>();

    // ── Items ────────────────────────────────────────────────────────

    public static readonly QueryDescription GroundItems =
        new QueryDescription().WithAll<Position, ItemData>();

    // ── FOV & Lighting ───────────────────────────────────────────────

    public static readonly QueryDescription FOVEntities =
        new QueryDescription().WithAll<Position, FOVData>();

    public static readonly QueryDescription LightSources =
        new QueryDescription().WithAll<Position, LightSource>();

    public static readonly QueryDescription PlayerFOV =
        new QueryDescription().WithAll<Position, FOVData, PlayerTag>();

    // ── Death & Cleanup ──────────────────────────────────────────────

    public static readonly QueryDescription DeadMonsters =
        new QueryDescription().WithAll<DeadTag, MonsterData>();

    public static readonly QueryDescription DeadMonstersWithPosition =
        new QueryDescription().WithAll<DeadTag, MonsterData, Position>();

    public static readonly QueryDescription DeadResourceNodes =
        new QueryDescription().WithAll<DeadTag, ResourceNodeData>();

    public static readonly QueryDescription DeadResourceNodesWithPosition =
        new QueryDescription().WithAll<DeadTag, ResourceNodeData, Position>();

    public static readonly QueryDescription DeadPlayers =
        new QueryDescription().WithAll<DeadTag, Health, PlayerTag, Position>();

    public static readonly QueryDescription DeadItems =
        new QueryDescription().WithAll<DeadTag, ItemData>();

    // ── Visible Entities (rendering/serialization) ───────────────────

    public static readonly QueryDescription TileAppearanceEntities =
        new QueryDescription().WithAll<Position, TileAppearance>();

    // ── Entity Persistence ───────────────────────────────────────────

    public static readonly QueryDescription SerializableMonsters =
        new QueryDescription().WithAll<Position, MonsterData, Health, AIState, MoveDelay, AttackDelay>().WithNone<PlayerTag, DeadTag>();

    public static readonly QueryDescription SerializableGroundItems =
        new QueryDescription().WithAll<Position, ItemData>().WithNone<PlayerTag, DeadTag, MonsterData, ResourceNodeData>();

    public static readonly QueryDescription SerializableResourceNodes =
        new QueryDescription().WithAll<Position, ResourceNodeData, Health, AttackDelay>().WithNone<PlayerTag, DeadTag>();

    public static readonly QueryDescription SerializableLitElements =
        new QueryDescription().WithAll<Position, TileAppearance, LightSource>().WithNone<PlayerTag, DeadTag, MonsterData, ItemData, ResourceNodeData, Health, TownNpcTag>();

    public static readonly QueryDescription SerializableElements =
        new QueryDescription().WithAll<Position, TileAppearance>().WithNone<PlayerTag, DeadTag, MonsterData, ItemData, ResourceNodeData, Health, LightSource, TownNpcTag>();

    public static readonly QueryDescription SerializableNpcs =
        new QueryDescription().WithAll<Position, TownNpcTag, Health, AIState, MoveDelay, AttackDelay>().WithNone<DeadTag>();
}
