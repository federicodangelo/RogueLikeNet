namespace RogueLikeNet.Core.Data;

public sealed class PlayerLevelTable
{
    private PlayerLevelDefinition[] _levels = [];

    public void Load(IEnumerable<PlayerLevelDefinition> levels)
    {
        _levels = levels.OrderBy(l => l.Level).ToArray();
    }

    public int MaxLevel => _levels.Length > 0 ? _levels[^1].Level : 1;

    public int Count => _levels.Length;

    public int GetXpRequired(int level)
    {
        foreach (var l in _levels)
            if (l.Level == level)
                return l.XpRequired;
        return int.MaxValue;
    }

    public int GetLevelForXp(int totalXp)
    {
        int level = 1;
        foreach (var l in _levels)
        {
            if (totalXp >= l.XpRequired)
                level = l.Level;
            else
                break;
        }
        return level;
    }

    public PlayerLevelDefinition[] All => _levels;
}
