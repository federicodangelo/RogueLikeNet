namespace RogueLikeNet.Core.Data;

/// <summary>
/// Holds all loaded NPC/monster definitions with O(1) lookup by numeric ID.
/// </summary>
public sealed class NpcRegistry
{
    private readonly Dictionary<string, NpcDefinition> _byStringId = new();
    private NpcDefinition?[] _byNumericId = [];

    public IReadOnlyCollection<NpcDefinition> All => _byStringId.Values;

    public void Register(IEnumerable<NpcDefinition> npcs, Func<string, int>? legacyIdProvider = null)
    {
        var sorted = npcs.OrderBy(n => n.Id, StringComparer.Ordinal).ToList();

        // Phase 1: assign legacy IDs
        int maxReserved = -1; // NPCs start from 0
        if (legacyIdProvider != null)
        {
            foreach (var npc in sorted)
            {
                int legacyId = legacyIdProvider(npc.Id);
                if (legacyId >= 0)
                {
                    npc.NumericId = legacyId;
                    maxReserved = Math.Max(maxReserved, legacyId);
                    npc.HasLegacyId = true;
                }
            }
        }

        // Phase 2: assign new IDs above legacy range
        int nextId = maxReserved + 1;
        foreach (var npc in sorted)
        {
            if (!npc.HasLegacyId)
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
