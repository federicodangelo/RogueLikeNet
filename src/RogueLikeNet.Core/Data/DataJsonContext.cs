using System.Text.Json;
using System.Text.Json.Serialization;

namespace RogueLikeNet.Core.Data;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(TileDefinition[]))]
[JsonSerializable(typeof(ItemDefinition[]))]
[JsonSerializable(typeof(RecipeDefinition[]))]
[JsonSerializable(typeof(ResourceNodeDefinition[]))]
[JsonSerializable(typeof(NpcDefinition[]))]
[JsonSerializable(typeof(BiomeDefinition[]))]
[JsonSerializable(typeof(AnimalDefinition[]))]
[JsonSerializable(typeof(ClassDataDefinition))]
[JsonSerializable(typeof(PlayerLevelDefinition[]))]
[JsonSerializable(typeof(StructureDefinition[]))]
[JsonSerializable(typeof(TownDefinition[]))]
[JsonSerializable(typeof(ShopDefinition[]))]
[JsonSerializable(typeof(SpellDefinition[]))]
internal partial class DataJsonContext : JsonSerializerContext;
