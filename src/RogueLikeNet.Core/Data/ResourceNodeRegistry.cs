namespace RogueLikeNet.Core.Data;

/// <summary>
/// Holds all loaded resource node definitions with O(1) lookup by numeric ID.
/// </summary>
public sealed class ResourceNodeRegistry
{
    private readonly Dictionary<string, ResourceNodeDefinition> _byStringId = new();
    private ResourceNodeDefinition?[] _byNumericId = [];

    public IReadOnlyCollection<ResourceNodeDefinition> All => _byStringId.Values;

    public void Register(IEnumerable<ResourceNodeDefinition> nodes)
    {
        var sorted = nodes.OrderBy(n => n.Id, StringComparer.Ordinal).ToList();

        // Assign sequential IDs starting from 1 (0 is reserved for None)
        int nextId = 1;
        foreach (var node in sorted)
        {
            node.NumericId = nextId++;
            _byStringId[node.Id] = node;
        }

        int maxId = sorted.Max(n => n.NumericId);
        _byNumericId = new ResourceNodeDefinition?[maxId + 1];
        foreach (var node in sorted)
            _byNumericId[node.NumericId] = node;
    }

    public ResourceNodeDefinition? Get(string id) =>
        _byStringId.GetValueOrDefault(id);

    public ResourceNodeDefinition? Get(int numericId) =>
        numericId > 0 && numericId < _byNumericId.Length ? _byNumericId[numericId] : null;

    public int Count => _byStringId.Count;
}
