namespace RogueLikeNet.Core.Systems;

public enum PlayerActionEventType
{
    PickUp = 0,
    Drop = 1,
    UsePotion = 2,
    EatFood = 3,
    Equip = 4,
    Unequip = 5,
    PlaceItem = 6,
    PickUpPlaced = 7,
    Till = 8,
    Plant = 9,
    Water = 10,
    Harvest = 11,
    FeedAnimal = 12,
    Craft = 13,
    LevelUp = 14,
    Kill = 15,
}

/// <summary>
/// Reason for a failed player action. Zero means no specific reason / generic failure.
/// </summary>
public enum ActionFailReason
{
    None = 0,
    InventoryFull = 1,
    NoItemsOnGround = 2,
    NothingToPickUp = 3,
}

public struct PlayerActionEvent
{
    public PlayerActionEventType EventType;
    public int ItemTypeId;
    public int StackCount;
    public bool Failed;
    public ActionFailReason FailReason;
    public int OldLevel;
    public int NewLevel;
    public int KilledNpcTypeId;
}
