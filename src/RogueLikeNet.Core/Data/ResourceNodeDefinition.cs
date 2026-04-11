using System.Text.Json.Serialization;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Defines a resource node type (tree, ore rock, etc.). Loaded from JSON data files.
/// </summary>
public sealed class ResourceNodeDefinition : BaseDefinition
{
    [JsonConverter(typeof(GlyphConverter))]
    public int GlyphId { get; set; }
    public int FgColor { get; set; }
    public int Health { get; set; }
    public int Defense { get; set; }
    public string DropItemId { get; set; } = "";
    public int MinDrop { get; set; } = 1;
    public int MaxDrop { get; set; } = 1;
    public ToolType RequiredToolType { get; set; }
}
