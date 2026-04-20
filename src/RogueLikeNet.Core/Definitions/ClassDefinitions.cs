using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Core.Definitions;

/// <summary>
/// Class-specific stat bonuses. Delegates to data-driven definitions loaded from JSON.
/// </summary>
public static class ClassDefinitions
{
    public const int InventorySlots = 30;
    public const int FOVRadius = 20;

    // Convenience constants matching JSON "order" values
    public const int Warrior = 0;
    public const int Rogue = 1;
    public const int Mage = 2;
    public const int Ranger = 3;

    private const int BaseHealth = 100;
    private const int BaseAttack = 10;
    private const int BaseDefense = 5;
    private const int BaseSpeed = 10;
    private const int BaseMana = 30;
    public static ClassStats BaseStats => new(BaseAttack, BaseDefense, BaseHealth, BaseSpeed);

    public static int NumClasses => GameData.Instance.Classes.ClassCount;

    public static ClassDataDefinition[] All => GameData.Instance.Classes.AllByIndex;

    public static ClassDataDefinition GetDef(int classIndex) =>
        GameData.Instance.Classes.GetByIndex(classIndex)!;

    public static ClassStats GetStartingStats(int classIndex)
    {
        var def = GetDef(classIndex);
        return new ClassStats(def.StartingStats.Attack, def.StartingStats.Defense, def.StartingStats.Health, def.StartingStats.Speed);
    }

    public static int GetStartingMana(int classIndex)
    {
        var def = GetDef(classIndex);
        return BaseMana + def.StartingStats.Mana;
    }

    public static string[] GetAsciiArt(int classIndex)
    {
        var def = GetDef(classIndex);
        return def.AsciiArt;
    }

    public static string GetName(int classIndex)
    {
        var def = GetDef(classIndex);
        return def.Name;
    }

    public static ClassStats GetLevelBonuses(int classIndex, int level)
    {
        var def = GetDef(classIndex);
        foreach (var bonus in def.LevelBonuses)
        {
            if (bonus.Level == level)
                return new ClassStats(bonus.Attack, bonus.Defense, bonus.Health, bonus.Speed);
        }
        // If no exact match, use highest level that doesn't exceed requested level
        ClassLevelBonus? best = null;
        foreach (var bonus in def.LevelBonuses)
        {
            if (bonus.Level <= level && (best == null || bonus.Level > best.Level))
                best = bonus;
        }
        if (best != null)
            return new ClassStats(best.Attack, best.Defense, best.Health, best.Speed);
        return default;
    }

    public static int GetLevelManaBonus(int classIndex, int level)
    {
        var def = GetDef(classIndex);
        foreach (var bonus in def.LevelBonuses)
        {
            if (bonus.Level == level)
                return bonus.Mana;
        }
        ClassLevelBonus? best = null;
        foreach (var bonus in def.LevelBonuses)
        {
            if (bonus.Level <= level && (best == null || bonus.Level > best.Level))
                best = bonus;
        }
        return best?.Mana ?? 0;
    }
}

public readonly record struct ClassStats(int Attack, int Defense, int Health, int Speed)
{
    public static ClassStats operator +(ClassStats a, ClassStats b) =>
        new(a.Attack + b.Attack, a.Defense + b.Defense, a.Health + b.Health, a.Speed + b.Speed);
};
