using System.Text.Json;
using System.Text.Json.Serialization;

namespace RogueLikeNet.Core.Data;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(ItemDefinition[]))]
[JsonSerializable(typeof(RecipeDefinition[]))]
[JsonSerializable(typeof(ResourceNodeDefinition[]))]
[JsonSerializable(typeof(NpcDefinition[]))]
[JsonSerializable(typeof(BiomeDefinition[]))]
[JsonSerializable(typeof(AnimalDefinition[]))]
internal partial class DataJsonContext : JsonSerializerContext;
