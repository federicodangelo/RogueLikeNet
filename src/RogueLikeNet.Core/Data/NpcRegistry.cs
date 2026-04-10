using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Holds all loaded NPC/monster definitions with O(1) lookup by numeric ID.
/// </summary>
public sealed class NpcRegistry
{
    private readonly Dictionary<string, NpcDefinition> _byStringId = new();
    private readonly Dictionary<int, NpcDefinition> _byNumericId = new();

    /// <summary>NPCs sorted by attack stat (ascending) for difficulty-gated picking.</summary>
    private NpcDefinition[] _sortedByAttack = [];

    public IReadOnlyCollection<NpcDefinition> All => _byStringId.Values;

    public void Register(IEnumerable<NpcDefinition> npcs)
    {
        var errors = new List<string>();

        foreach (var npc in npcs)
        {
            npc.NumericId = DefinitionIdHash.Compute(npc.Id);

            if (!_byStringId.TryAdd(npc.Id, npc))
                errors.Add($"NPC: duplicate string ID '{npc.Id}'.");

            if (!_byNumericId.TryAdd(npc.NumericId, npc))
                errors.Add($"NPC '{npc.Id}': hash collision on NumericId {npc.NumericId}.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"NpcRegistry validation failed:\n" + string.Join("\n", errors));

        _sortedByAttack = [.. _byStringId.Values.OrderBy(n => n.Attack).ThenBy(n => n.Id, StringComparer.Ordinal)];
    }

    public NpcDefinition? Get(string id) =>
        _byStringId.GetValueOrDefault(id);

    public NpcDefinition? Get(int numericId) =>
        _byNumericId.GetValueOrDefault(numericId);

    public int Count => _byStringId.Count;

    /// <summary>
    /// Picks a random monster type suitable for the given difficulty tier (0-based).
    /// NPCs are sorted by attack; higher difficulty unlocks more of the sorted list.
    /// </summary>
    public NpcDefinition? Pick(SeededRandom rng, int difficulty)
    {
        if (_sortedByAttack.Length == 0) return null;

        int maxIndex = Math.Min(difficulty + 1, _sortedByAttack.Length - 1);
        int idx = rng.Next(maxIndex + 1);
        return _sortedByAttack[idx];
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
