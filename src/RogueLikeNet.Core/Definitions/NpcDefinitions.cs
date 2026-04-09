using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Definitions;

/// <summary>
/// NPC/monster definitions. Derives data from the GameData NPC registry.
/// </summary>
public static class NpcDefinitions
{
    /// <summary>All known NPC definitions from the registry.</summary>
    public static NpcDef[] All =>
        [.. GameData.Instance.Npcs.All.Select(d => ConvertFromData(d))];
    /// <summary>
    /// Lookup by NumericId. Returns data from the JSON registry.
    /// </summary>
    public static NpcDef Get(int typeId)
    {
        var d = GameData.Instance.Npcs.Get(typeId);
        if (d != null)
            return ConvertFromData(d);
        return default;
    }

    private static NpcDef ConvertFromData(Data.NpcDefinition d) =>
        new(d.NumericId, d.Name, d.GlyphId, d.FgColor, d.Health, d.Attack, d.Defense, d.Speed);

    /// <summary>
    /// Picks a random monster type suitable for the given difficulty tier (0-based).
    /// </summary>
    public static NpcDef Pick(SeededRandom rng, int difficulty)
    {
        var npcReg = GameData.Instance.Npcs;
        if (npcReg.Count > 0)
        {
            int totalCount = npcReg.Count;
            int maxIndex = Math.Min(difficulty + 1, totalCount - 1);
            int idx = rng.Next(maxIndex + 1);
            var npc = npcReg.Get(idx);
            if (npc != null) return ConvertFromData(npc);
        }

        return default;
    }

    public static MonsterData GenerateMonsterData(NpcDef def, int difficulty)
    {
        int bonusHealth = def.Health * (difficulty / 2);
        int bonusAttack = difficulty;
        int bonusDefense = difficulty / 2;
        int bonusSpeed = 0;

        return new MonsterData
        {
            MonsterTypeId = def.TypeId,
            Health = def.Health + bonusHealth,
            Attack = def.Attack + bonusAttack,
            Defense = def.Defense + bonusDefense,
            Speed = def.Speed + bonusSpeed,
        };
    }
}

public readonly record struct NpcDef(
    int TypeId, string Name, int GlyphId, int Color,
    int Health, int Attack, int Defense, int Speed
);
