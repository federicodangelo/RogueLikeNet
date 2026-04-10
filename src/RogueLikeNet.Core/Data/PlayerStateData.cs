namespace RogueLikeNet.Core.Data;

public class PlayerStateData
{
    public int Health;
    public int MaxHealth;
    public int Attack;
    public int Defense;
    public int Level;
    public int Experience;
    public int Hunger;
    public int MaxHunger;
    public int Thirst;
    public int MaxThirst;
    public int InventoryCount;
    public int InventoryCapacity;
    public SkillSlotData[] Skills = [];
    public InventoryItemData[] InventoryItems = [];
    public InventoryItemData[] EquippedItems = [];
    public int[] QuickSlotIndices = [];
    public int[] NearbyStationsTypes = [];
}
