namespace RogueLikeNet.Core.Data;

/// <summary>
/// Holds all loaded recipe definitions and provides lookups by station type or numeric ID.
/// </summary>
public sealed class RecipeRegistry
{
    private readonly Dictionary<string, RecipeDefinition> _byStringId = new();
    private RecipeDefinition?[] _byNumericId = [];
    private readonly Dictionary<CraftingStationType, List<RecipeDefinition>> _byStation = new();

    public IReadOnlyCollection<RecipeDefinition> All => _byStringId.Values;

    public void Register(IEnumerable<RecipeDefinition> recipes, ItemRegistry itemRegistry)
    {
        var sorted = recipes.OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
        int nextId = 0;
        foreach (var recipe in sorted)
        {
            recipe.NumericId = nextId++;
            _byStringId[recipe.Id] = recipe;

            if (!_byStation.TryGetValue(recipe.Station, out var list))
            {
                list = new List<RecipeDefinition>();
                _byStation[recipe.Station] = list;
            }
            list.Add(recipe);
        }

        _byNumericId = new RecipeDefinition?[nextId];
        foreach (var recipe in sorted)
            _byNumericId[recipe.NumericId] = recipe;
    }

    public RecipeDefinition? Get(string id) =>
        _byStringId.GetValueOrDefault(id);

    public RecipeDefinition? Get(int numericId) =>
        numericId >= 0 && numericId < _byNumericId.Length ? _byNumericId[numericId] : null;

    public IReadOnlyList<RecipeDefinition> GetByStation(CraftingStationType station) =>
        _byStation.TryGetValue(station, out var list) ? list : [];

    public int Count => _byStringId.Count;
}
