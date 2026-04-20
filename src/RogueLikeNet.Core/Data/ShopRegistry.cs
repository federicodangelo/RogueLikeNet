namespace RogueLikeNet.Core.Data;

/// <summary>
/// Registry for shop definitions, indexed by TownNpcRole.
/// </summary>
public sealed class ShopRegistry
{
    private readonly Dictionary<TownNpcRole, ShopDefinition> _shopsByRole = new();
    private readonly List<ShopDefinition> _all = [];

    public int Count => _all.Count;

    public void Register(IEnumerable<ShopDefinition> shops)
    {
        foreach (var shop in shops)
        {
            _shopsByRole[shop.Role] = shop;
            _all.Add(shop);
        }
    }

    public ShopDefinition? GetByRole(TownNpcRole role)
    {
        _shopsByRole.TryGetValue(role, out var shop);
        return shop;
    }

    public IReadOnlyList<ShopDefinition> All => _all;
}
