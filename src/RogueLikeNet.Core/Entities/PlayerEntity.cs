using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Systems;

namespace RogueLikeNet.Core.Entities;

public struct PlayerEntity
{
    public PlayerEntity(int id) { Id = id; }
    public readonly int Id;
    public int ServerPlayerId;
    public Position Position;
    public Health Health;
    public long ConnectionId;
    public CombatStats CombatStats;
    public FOVData FOV;
    public TileAppearance Appearance;
    public PlayerInput Input;
    public ClassData ClassData;
    public Inventory Inventory;
    public Equipment Equipment;
    public QuickSlots QuickSlots;
    public MoveDelay MoveDelay;
    public AttackDelay AttackDelay;
    public Survival Survival;
    public Mana Mana;
    public ActiveEffects ActiveEffects;
    public List<PlayerActionEvent> ActionEvents = new();
    public bool IsDead => !Health.IsAlive;

    public PlayerEntity()
    {
        Inventory = new Inventory();
        Survival = Survival.Default();
    }
}
