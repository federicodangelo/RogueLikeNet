using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.World;

public struct EntityRef
{
    public const int NullId = 0;

    public readonly int Id;
    public readonly EntityType Type;

    public EntityRef(int id, EntityType type)
    {
        Id = id;
        Type = type;
    }
}

public struct MonsterEntity
{
    public MonsterEntity(int id) { Id = id; }
    public readonly int Id;
    public Position Position;
    public Health Health;
    public MonsterData MonsterData;
    public CombatStats CombatStats;
    public TileAppearance Appearance;
    public AIState AI;
    public MoveDelay MoveDelay;
    public AttackDelay AttackDelay;
    public bool IsDead => !Health.IsAlive;
}

public struct GroundItemEntity
{
    public GroundItemEntity(int id) { Id = id; }
    public readonly int Id;
    public Position Position;
    public TileAppearance Appearance;
    public ItemData Item;
    public bool IsDestroyed;
}

public struct ResourceNodeEntity
{
    public ResourceNodeEntity(int id) { Id = id; }
    public readonly int Id;
    public Position Position;
    public Health Health;
    public CombatStats CombatStats;
    public TileAppearance Appearance;
    public ResourceNodeData NodeData;
    public AttackDelay AttackDelay;
    public bool IsDead => !Health.IsAlive;
}

public struct TownNpcEntity
{
    public TownNpcEntity(int id) { Id = id; }
    public readonly int Id;
    public Position Position;
    public Health Health;
    public CombatStats CombatStats;
    public TileAppearance Appearance;
    public AIState AI;
    public TownNpcTag NpcData;
    public MoveDelay MoveDelay;
    public AttackDelay AttackDelay;
    public bool IsDead => !Health.IsAlive;
}

public struct ElementEntity
{
    public ElementEntity(int id) { Id = id; }
    public readonly int Id;
    public Position Position;
    public TileAppearance Appearance;
    public LightSource? Light;
}

public struct PlayerEntity
{
    public PlayerEntity(int id) { Id = id; }
    public readonly int Id;
    public Position Position;
    public Health Health;
    public long ConnectionId;
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
    public bool IsDead => !Health.IsAlive;

    public PlayerEntity()
    {
        Inventory = new Inventory();
    }
}
