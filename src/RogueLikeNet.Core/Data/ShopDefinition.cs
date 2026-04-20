namespace RogueLikeNet.Core.Data;

/// <summary>
/// Defines the inventory of a shop associated with a TownNpcRole.
/// Each shop has a list of items for sale with base prices.
/// </summary>
public sealed class ShopDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public TownNpcRole Role { get; set; }
    public ShopItemEntry[] Items { get; set; } = [];
    public int SellPricePercent { get; set; } = 50;
}

public sealed class ShopItemEntry
{
    public string ItemId { get; set; } = "";
    public int Price { get; set; }
    public int Stock { get; set; } = -1; // -1 = infinite
}
