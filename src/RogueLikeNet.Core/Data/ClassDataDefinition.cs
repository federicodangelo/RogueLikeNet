using System.Text.Json.Serialization;

namespace RogueLikeNet.Core.Data;

public class ClassLevelBonus
{
    public int Level { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Health { get; set; }
    public int Speed { get; set; }
}

public class ClassStartingStats
{
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Health { get; set; }
    public int Speed { get; set; }
}

public class ClassDataDefinition : BaseDefinition
{
    public int Order { get; set; }
    public ClassStartingStats StartingStats { get; set; } = new();
    public string[] AsciiArt { get; set; } = [];
    public ClassLevelBonus[] LevelBonuses { get; set; } = [];

    [JsonIgnore]
    public int ClassIndex { get; set; }
}
