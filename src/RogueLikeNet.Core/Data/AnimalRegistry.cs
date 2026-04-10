namespace RogueLikeNet.Core.Data;

/// <summary>
/// Holds all loaded animal definitions with O(1) lookup by numeric ID.
/// </summary>
public sealed class AnimalRegistry : BaseRegistry<AnimalDefinition>
{
}
