using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Definitions;

/// <summary>
/// Definitions for peaceful town NPCs: names, appearances, and dialogue lines.
/// </summary>
public static class TownNpcDefinitions
{
    public static readonly string[] Names =
    [
        "Elara", "Brin", "Thom", "Mira", "Gendry",
        "Sera", "Orin", "Lysa", "Finn", "Dara",
    ];

    public static readonly string[] Dialogues =
    [
        "Welcome to our town, traveler!",
        "The caves beyond are dangerous. Be careful.",
        "I heard there's gold deep underground.",
        "Nice weather today, isn't it?",
        "Have you tried the local mushroom stew?",
        "My neighbor found a rare crystal yesterday.",
        "The dragons have been restless lately...",
        "Need supplies? Try crafting at a workbench.",
        "I used to be an adventurer, once.",
        "Watch out for skeletons in the crypt biome.",
        "These walls keep us safe at night.",
        "Trade is slow since the monsters appeared.",
    ];

    public static string PickName(SeededRandom rng) => Names[rng.Next(Names.Length)];
    public static int PickDialogue(SeededRandom rng) => rng.Next(Dialogues.Length);
}
