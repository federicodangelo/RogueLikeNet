namespace RogueLikeNet.Core.Data;

/// <summary>
/// Registry for structure templates. Provides lookup by category.
/// </summary>
public sealed class StructureRegistry : BaseRegistry<StructureDefinition>
{
    private readonly Dictionary<string, List<StructureDefinition>> _byCategory = new(StringComparer.OrdinalIgnoreCase);

    protected override void ExtraRegister(StructureDefinition def)
    {
        if (!_byCategory.TryGetValue(def.Category, out var list))
        {
            list = [];
            _byCategory[def.Category] = list;
        }
        list.Add(def);
    }

    public IReadOnlyList<StructureDefinition> GetByCategory(string category) =>
        _byCategory.TryGetValue(category, out var list) ? list : [];

    public IReadOnlyList<StructureDefinition> GetByCategoryOrIds(string category, string[]? structureIds)
    {
        if (structureIds is { Length: > 0 })
        {
            var result = new List<StructureDefinition>();
            foreach (var id in structureIds)
            {
                var def = Get(id);
                if (def != null)
                    result.Add(def);
            }
            return result;
        }
        return GetByCategory(category);
    }
}
