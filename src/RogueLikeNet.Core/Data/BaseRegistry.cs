namespace RogueLikeNet.Core.Data;

public class BaseRegistry<T> where T : BaseDefinition
{
    protected readonly Dictionary<string, T> _byStringId = [];
    protected readonly Dictionary<int, T> _byNumericId = [];

    public IReadOnlyCollection<T> All => _byStringId.Values;
    public int Count => _byStringId.Count;

    public void Register(IEnumerable<T> definitions)
    {
        var errors = new List<string>();

        foreach (var def in definitions)
        {
            def.NumericId = DefinitionIdHash.Compute(def.Id);

            if (!_byStringId.TryAdd(def.Id, def))
                errors.Add($"Biome: duplicate string ID '{def.Id}'.");

            if (!_byNumericId.TryAdd(def.NumericId, def))
                errors.Add($"Biome '{def.Id}': hash collision on NumericId {def.NumericId}.");

            ExtraRegister(def);
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"BiomeRegistry validation failed:\n" + string.Join("\n", errors));

        PostRegister();
    }

    protected virtual void ExtraRegister(T definition) { }

    protected virtual void PostRegister() { }

    public T? Get(string id) =>
        _byStringId.GetValueOrDefault(id);

    public T? Get(int numericId) =>
        _byNumericId.GetValueOrDefault(numericId);

    public int GetNumericId(string id) =>
        _byStringId.TryGetValue(id, out var def) ? def.NumericId : 0;
}
