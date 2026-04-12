namespace RogueLikeNet.Core.Data;

public sealed class ClassRegistry : BaseRegistry<ClassDataDefinition>
{
    private ClassDataDefinition[] _byIndex = [];

    protected override void PostRegister()
    {
        var sorted = _byStringId.Values.OrderBy(c => c.Order).ToList();
        for (int i = 0; i < sorted.Count; i++)
            sorted[i].ClassIndex = i;
        _byIndex = sorted.ToArray();
    }

    public ClassDataDefinition? GetByIndex(int index) =>
        index >= 0 && index < _byIndex.Length ? _byIndex[index] : null;

    public int ClassCount => _byIndex.Length;

    public ClassDataDefinition[] AllByIndex => _byIndex;
}
