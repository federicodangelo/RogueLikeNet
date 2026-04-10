using System.Text.Json.Serialization;

namespace RogueLikeNet.Core.Data;

public class BaseDefinition
{
    public string Id { get; set; } = "";
    [JsonIgnore]
    public int NumericId { get; set; }
    public string Name { get; set; } = "";
}
