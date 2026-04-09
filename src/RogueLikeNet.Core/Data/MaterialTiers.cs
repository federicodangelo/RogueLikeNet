namespace RogueLikeNet.Core.Data;

/// <summary>
/// Provides material-tier-based stat multipliers.
/// Items derive their effective stats from base stats × material tier multiplier.
/// </summary>
public static class MaterialTiers
{
    /// <summary>
    /// Returns the damage/defense multiplier for a material tier as a percentage (100 = 1.0x).
    /// </summary>
    public static int GetMultiplier(MaterialTier tier) => tier switch
    {
        MaterialTier.Wood => 100,
        MaterialTier.Stone => 130,
        MaterialTier.Copper => 160,
        MaterialTier.Iron => 200,
        MaterialTier.Steel => 250,
        MaterialTier.Gold => 150,
        MaterialTier.Mithril => 300,
        MaterialTier.Adamantite => 400,
        _ => 100,
    };

    /// <summary>
    /// Computes effective stat = baseStat * tierMultiplier / 100.
    /// </summary>
    public static int Apply(int baseStat, MaterialTier tier) =>
        baseStat * GetMultiplier(tier) / 100;
}
