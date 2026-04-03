using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.World;

public class Entity
{
    public readonly int Id;
    public readonly EntityType Type;
    public int X, Y, Z;

    public Entity(int id, EntityType type)
    {
        Id = id;
        Type = type;
    }
}

public class EntityWithHealth : Entity
{
    public EntityWithHealth(int id, EntityType type) : base(id, type) { }
    public Health Health;
    public bool IsDead;
}

public class MonsterEntity : EntityWithHealth
{
    public MonsterEntity(int id) : base(id, EntityType.Monster) { }
    public MonsterData MonsterData;
    public CombatStats CombatStats;
    public TileAppearance Appearance;
    public AIState AI;
    public MoveDelay MoveDelay;
    public AttackDelay AttackDelay;
}

public class GroundItemEntity : Entity
{
    public GroundItemEntity(int id) : base(id, EntityType.GroundItem) { }
    public TileAppearance Appearance;
    public ItemData Item;
    public bool IsDead;
}

public class ResourceNodeEntity : EntityWithHealth
{
    public ResourceNodeEntity(int id) : base(id, EntityType.ResourceNode) { }
    public CombatStats CombatStats;
    public TileAppearance Appearance;
    public ResourceNodeData NodeData;
    public AttackDelay AttackDelay;
}

public class TownNpcEntity : EntityWithHealth
{
    public TownNpcEntity(int id) : base(id, EntityType.TownNpc) { }
    public CombatStats CombatStats;
    public TileAppearance Appearance;
    public AIState AI;
    public TownNpcTag NpcData;
    public MoveDelay MoveDelay;
    public AttackDelay AttackDelay;
}

public class ElementEntity : Entity
{
    public ElementEntity(int id) : base(id, EntityType.Element) { }
    public TileAppearance Appearance;
    public LightSource? Light;
}

public class PlayerEntity : EntityWithHealth
{
    public PlayerEntity(int id) : base(id, EntityType.Player) { }
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
}
