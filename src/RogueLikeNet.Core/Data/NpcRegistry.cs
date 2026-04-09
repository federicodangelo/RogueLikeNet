namespace RogueLikeNet.Core.Data;

/// <summary>
/// Holds all loaded NPC/monster definitions with O(1) lookup by numeric ID.
/// </summary>
public sealed class NpcRegistry
{
    private readonly Dictionary<string, NpcDefinition> _byStringId = new();
    private NpcDefinition?[] _byNumericId = [];

    public IReadOnlyCollection<NpcDefinition> All => _byStringId.Values;

    public void Register(IEnumerable<NpcDefinition> npcs)
    {
        var sorted = npcs.OrderBy(n => n.Id, StringComparer.Ordinal).ToList();

        // Assign sequential IDs starting from 0
        int nextId = 0;
        foreach (var npc in sorted)
        {
            npc.NumericId = nextId++;
            _byStringId[npc.Id] = npc;
        }

        int maxId = sorted.Max(n => n.NumericId);
        _byNumericId = new NpcDefinition?[maxId + 1];
        foreach (var npc in sorted)
            _byNumericId[npc.NumericId] = npc;
    }

    public NpcDefinition? Get(string id) =>
        _byStringId.GetValueOrDefault(id);

    public NpcDefinition? Get(int numericId) =>
        numericId >= 0 && numericId < _byNumericId.Length ? _byNumericId[numericId] : null;

    public int Count => _byStringId.Count;
}
