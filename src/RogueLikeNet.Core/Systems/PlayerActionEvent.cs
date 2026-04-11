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
}

public struct PlayerActionEvent
{
    public PlayerActionEventType EventType;
    public int ItemTypeId;
    public int StackCount;
}
