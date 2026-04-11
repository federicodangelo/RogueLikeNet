using System.Text.Json.Serialization;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Defines an animal type (chicken, cow, sheep, etc.). Loaded from JSON data files.
/// </summary>
public sealed class AnimalDefinition : BaseDefinition
{
    [JsonConverter(typeof(GlyphConverter))]
    public int GlyphId { get; set; }
    public int FgColor { get; set; }
    public int Health { get; set; }
    public string ProduceItemId { get; set; } = "";
    public int ProduceIntervalTicks { get; set; } = 600;
    public string FeedItemId { get; set; } = "";
    public int FedDurationTicks { get; set; } = 1200;
    public int BreedCooldownTicks { get; set; } = 2400;
}
