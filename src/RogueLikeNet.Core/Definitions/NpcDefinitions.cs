using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Definitions;

/// <summary>
/// NPC/monster definitions indexed by type ID. All values are integers.
/// </summary>
public static class NpcDefinitions
{
    public const int Goblin = 0;
    public const int Orc = 1;
    public const int Skeleton = 2;
    public const int Dragon = 3;

    public static readonly NpcDefinition[] All =
    [
        new(Goblin,   "Goblin",   TileDefinitions.GlyphGoblin,   TileDefinitions.ColorGreen,   15, 4, 1, 3),
        new(Orc,      "Orc",      TileDefinitions.GlyphOrc,      TileDefinitions.ColorRed,     30, 7, 3, 2),
        new(Skeleton, "Skeleton", TileDefinitions.GlyphSkeleton, TileDefinitions.ColorWhite,   20, 5, 2, 3),
        new(Dragon,   "Dragon",   TileDefinitions.GlyphDragon,   TileDefinitions.ColorOrange, 100, 15, 8, 1),
    ];

    // Maps old numeric TypeId → new string ID for JSON lookup
    private static readonly string[] OldToNewNpcId = ["goblin", "orc", "skeleton", "dragon"];

    /// <summary>
    /// Lookup by TypeId. When GameData is loaded, returns data from the JSON registry.
    /// Otherwise falls back to the hardcoded array.
    /// </summary>
    public static NpcDefinition Get(int typeId)
    {
        var npcReg = GameData.Instance.Npcs;
        if (npcReg.Count > 0 && typeId >= 0)
        {
            // Try legacy string lookup first
            if (typeId < OldToNewNpcId.Length)
            {
                var d = npcReg.Get(OldToNewNpcId[typeId]);
                if (d != null)
                    return ConvertFromData(typeId, d);
            }
            else
            {
                // Direct registry lookup by NumericId (for new NPCs without legacy mapping)
                var d = npcReg.Get(typeId);
                if (d != null)
                    return ConvertFromData(typeId, d);
            }
        }

        return Array.Find(All, d => d.TypeId == typeId);
    }

    private static NpcDefinition ConvertFromData(int typeId, Data.NpcDefinition d) =>
        new(typeId, d.Name, d.GlyphId, d.FgColor, d.Health, d.Attack, d.Defense, d.Speed);

    /// <summary>
    /// Picks a random monster type suitable for the given difficulty tier (0-based).
    /// When GameData is loaded, includes new NPC types from the registry.
    /// </summary>
    public static NpcDefinition Pick(SeededRandom rng, int difficulty)
    {
        var npcReg = GameData.Instance.Npcs;
        if (npcReg.Count > 0)
        {
            // Use all registered NPCs, gated by difficulty
            int totalCount = npcReg.Count;
            int maxIndex = Math.Min(difficulty + 1, totalCount - 1);
            int idx = rng.Next(maxIndex + 1);
            // Get the NPC at this index (sorted alphabetically by ID during registration)
            var npc = npcReg.Get(idx);
            if (npc != null) return ConvertFromData(idx, npc);
        }

        // Fallback: Higher difficulty unlocks harder monsters
        int maxIdx = Math.Min(difficulty + 1, All.Length - 1);
        return All[rng.Next(maxIdx + 1)];
    }

    public static MonsterData GenerateMonsterData(NpcDefinition def, int difficulty)
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

public readonly record struct NpcDefinition(
    int TypeId, string Name, int GlyphId, int Color,
    int Health, int Attack, int Defense, int Speed
);
