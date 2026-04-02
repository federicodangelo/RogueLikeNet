using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.World;

/// <summary>Lightweight reference to any entity by ID and type.</summary>
public readonly struct EntityRef
{
    public readonly int Id;
    public readonly EntityType Type;

    public EntityRef(int id, EntityType type)
    {
        Id = id;
        Type = type;
    }
}

public class MonsterEntity
{
    public int Id;
    public int X, Y, Z;
    public MonsterData MonsterData;
    public Health Health;
    public CombatStats CombatStats;
    public TileAppearance Appearance;
    public AIState AI;
    public MoveDelay MoveDelay;
    public AttackDelay AttackDelay;
    public bool IsDead;
}

public class GroundItemEntity
{
    public int Id;
    public int X, Y, Z;
    public TileAppearance Appearance;
    public ItemData Item;
    public bool IsDead;
}

public class ResourceNodeEntity
{
    public int Id;
    public int X, Y, Z;
    public Health Health;
    public CombatStats CombatStats;
    public TileAppearance Appearance;
    public ResourceNodeData NodeData;
    public AttackDelay AttackDelay;
    public bool IsDead;
}

public class TownNpcEntity
{
    public int Id;
    public int X, Y, Z;
    public Health Health;
    public CombatStats CombatStats;
    public TileAppearance Appearance;
    public AIState AI;
    public TownNpcTag NpcData;
    public MoveDelay MoveDelay;
    public AttackDelay AttackDelay;
    public bool IsDead;
}

public class ElementEntity
{
    public int Id;
    public int X, Y, Z;
    public TileAppearance Appearance;
    public LightSource? Light;
}

public class PlayerEntity
{
    public int Id;
    public long ConnectionId;
    public int X, Y, Z;
    public Health Health;
    public CombatStats CombatStats;
    public FOVData FOV;
    public TileAppearance Appearance;
    public PlayerInput Input;
    public ClassData ClassData;
    public SkillSlots Skills;
    public Inventory Inventory;
    public Equipment Equipment;
    public QuickSlots QuickSlots;
    public MoveDelay MoveDelay;
    public AttackDelay AttackDelay;
    public bool IsDead;
}
