namespace RogueLikeNet.Core.Components;

public struct PlayerInput
{
    public int ActionType;
    public int TargetX;
    public int TargetY;
    public int ItemSlot;
    public int TargetSlot;
    public int TargetNpcEntityId;
    public int TargetQuestId;
}

public static class ActionTypes
{
    public const int None = 0;
    public const int Move = 1;
    public const int Attack = 2;
    public const int UseItem = 3;
    public const int PickUp = 5;
    public const int Drop = 6;
    public const int Wait = 7;
    public const int SwapItems = 8;
    public const int Unequip = 9;
    //public const int Equip = 10; // Replaced by context-sensitive UseItem action that the server resolves to Equip
    public const int SetQuickSlot = 11;
    public const int UseQuickSlot = 12;
    public const int Craft = 13;
    public const int PlaceItem = 14;
    public const int PickUpPlaced = 15;
    public const int UseStairs = 16;
    public const int Till = 17;
    public const int Plant = 18;
    public const int Water = 19;
    public const int Harvest = 20;
    public const int FeedAnimal = 21;
    public const int Interact = 22;         // Context-sensitive: server resolves to Till/Plant/Water/Harvest/Feed
    public const int DropEquipped = 23;      // Unequip + drop to ground in one action
    public const int BuyItem = 24;           // Buy item from shop (ItemSlot = shop entry index)
    public const int SellItem = 25;          // Sell item from inventory (ItemSlot = inventory slot)
    public const int CastSpell = 26;         // Cast spell (ItemSlot = spell numeric ID)
    public const int InteractNpc = 27;       // Bump-to-talk: server returns NpcInteractionMsg (TargetNpcEntityId required)
    public const int AcceptQuest = 28;       // Accept quest from NPC (TargetNpcEntityId, TargetQuestId)
    public const int DeclineQuest = 29;      // Decline quest (no state change, client-only confirmation)
    public const int TurnInQuest = 30;       // Turn in completed quest (TargetNpcEntityId, TargetQuestId)
    public const int AbandonQuest = 31;      // Abandon active quest (TargetQuestId)
}
