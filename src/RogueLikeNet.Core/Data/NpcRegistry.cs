using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Holds all loaded NPC/monster definitions with O(1) lookup by numeric ID.
/// </summary>
public sealed class NpcRegistry : BaseRegistry<NpcDefinition>
{
    /// <summary>NPCs sorted by attack stat (ascending) for difficulty-gated picking.</summary>
    private NpcDefinition[] _sortedByAttack = [];

    protected override void PostRegister()
    {
        _sortedByAttack = [.. _byStringId.Values.OrderBy(n => n.Attack).ThenBy(n => n.Id, StringComparer.Ordinal)];
    }

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
