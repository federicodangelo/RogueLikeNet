namespace RogueLikeNet.Core.Data;

/// <summary>
/// Holds all loaded item definitions and provides O(1) lookup by numeric ID or string ID.
/// </summary>
public sealed class ItemRegistry
{
    private readonly Dictionary<string, ItemDefinition> _byStringId = new();
    private ItemDefinition?[] _byNumericId = [];

    public IReadOnlyCollection<ItemDefinition> All => _byStringId.Values;

    public void Register(IEnumerable<ItemDefinition> items)
    {
        // Sort by string ID for deterministic numeric ID assignment
        var sorted = items.OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
        int nextId = 1; // 0 reserved for None
        foreach (var item in sorted)
        {
            item.NumericId = nextId++;
            _byStringId[item.Id] = item;
        }

        _byNumericId = new ItemDefinition?[nextId];
        foreach (var item in sorted)
            _byNumericId[item.NumericId] = item;
    }

    public ItemDefinition? Get(string id) =>
        _byStringId.GetValueOrDefault(id);

    public ItemDefinition? Get(int numericId) =>
        numericId > 0 && numericId < _byNumericId.Length ? _byNumericId[numericId] : null;

    public int GetNumericId(string id) =>
        _byStringId.TryGetValue(id, out var def) ? def.NumericId : 0;

    public int Count => _byStringId.Count;
}
