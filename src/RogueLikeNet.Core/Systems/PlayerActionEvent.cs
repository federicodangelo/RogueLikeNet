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
    Buy = 16,
    Sell = 17,
    CastSpell = 18,
    Gather = 19,
    QuestAccepted = 20,
    QuestObjectiveAdvanced = 21,
    QuestCompleted = 22,
    QuestAbandoned = 23,
    QuestActionFailed = 24,
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
    InsufficientMana = 4,
    SpellOnCooldown = 5,
    QuestNotAvailable = 6,
    QuestAlreadyActive = 7,
    QuestNotComplete = 8,
    QuestWrongGiver = 9,
    QuestTooFar = 10,
    QuestMissingItems = 11,
    QuestCapacityFull = 12,
    QuestNotFound = 13,
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
    public int QuestNumericId;
    public int QuestObjectiveIndex;
    public int ObjectiveCurrent;
    public int ObjectiveTarget;
}
