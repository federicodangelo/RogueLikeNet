using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;

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

    /// <summary>
    /// Picks a random monster type suitable for the given difficulty tier (0-based).
    /// </summary>
    public NpcDefinition? Pick(SeededRandom rng, int difficulty)
    {
        if (Count > 0)
        {
            int totalCount = Count;
            int maxIndex = Math.Min(difficulty + 1, totalCount - 1);
            int idx = rng.Next(maxIndex + 1);
            var npc = Get(idx);
            if (npc != null) return npc;
        }

        return null;
    }

    public static MonsterData GenerateMonsterData(NpcDefinition def, int difficulty)
    {
        int bonusHealth = def.Health * (difficulty / 2);
        int bonusAttack = difficulty;
        int bonusDefense = difficulty / 2;
        int bonusSpeed = 0;

        return new MonsterData
        {
            MonsterTypeId = def.NumericId,
            Health = def.Health + bonusHealth,
            Attack = def.Attack + bonusAttack,
            Defense = def.Defense + bonusDefense,
            Speed = def.Speed + bonusSpeed,
        };
    }
}
