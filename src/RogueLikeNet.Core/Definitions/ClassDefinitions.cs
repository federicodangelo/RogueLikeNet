using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Definitions;

/// <summary>
/// Class-specific stat bonuses
/// </summary>
public static class ClassDefinitions
{
    public const int InventorySlots = 20;
    public const int FOVRadius = 20;

    public const int Warrior = 0;
    public const int Rogue = 1;
    public const int Mage = 2;
    public const int Ranger = 3;

    public const int NumClasses = 4;


    private const int BaseHealth = 100;
    private const int BaseAttack = 10;
    private const int BaseDefense = 5;
    private const int BaseSpeed = 10;
    public static ClassStats BaseStats => new(BaseAttack, BaseDefense, BaseHealth, BaseSpeed);

    public static readonly ClassDefinition[] All =
    [
        // Warrior - sword and shield
        new(Warrior, "Warrior", new ClassStats(3, 3, 20, 0),
        [
            @"    O/  ",
            @"   /[+] ",
            @"    /|  ",
            @"   / |  ",
            @"  _/ \_ ",
        ]),
        // Rogue - dual daggers
        new(Rogue,   "Rogue",   new ClassStats(1, 0, 0, 4),
        [
            @"   _O_  ",
            @"  /- -\ ",
            @"   \|/  ",
            @"   /|\  ",
            @"  _/ \_ ",
        ]),
        // Mage - staff and spell
        new(Mage,    "Mage",    new ClassStats(0, 0, -10, 2),
        [
            @"   \O/  ",
            @"  .*|*. ",
            @"    |   ",
            @"   /|\  ",
            @"  _/ \_ ",
        ]),
        // Ranger - bow
        new(Ranger,  "Ranger",  new ClassStats(2, 1, 0, 2),
        [
            @"    O}  ",
            @"   )|}  ",
            @"    |}  ",
            @"   /|   ",
            @"  _/ \_ ",
        ]),
    ];

    public static ClassDefinition Get(int classId) =>
        Array.Find(All, d => d.ClassId == classId);

    /// <summary>Returns (bonusAttack, bonusDefense, bonusHealth, bonusSpeed).</summary>
    public static ClassStats GetStartingStats(int classId)
    {
        var def = Get(classId);
        return def.StartingStats;
    }

    public static string[] GetAsciiArt(int classId)
    {
        var def = Get(classId);
        return def.AsciiArt;
    }
}

public readonly record struct ClassStats(int Attack, int Defense, int Health, int Speed)
{
    public static ClassStats operator +(ClassStats a, ClassStats b) =>
        new(a.Attack + b.Attack, a.Defense + b.Defense, a.Health + b.Health, a.Speed + b.Speed);
};

public readonly record struct ClassDefinition(int ClassId, string Name, ClassStats StartingStats, string[] AsciiArt);
