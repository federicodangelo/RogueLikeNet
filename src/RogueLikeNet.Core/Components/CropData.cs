namespace RogueLikeNet.Core.Components;

public struct CropData
{
    /// <summary>Numeric ID of the seed item that was planted.</summary>
    public int SeedItemTypeId;

    /// <summary>Numeric ID of the item produced on harvest.</summary>
    public int HarvestItemTypeId;

    public int HarvestMin;
    public int HarvestMax;

    /// <summary>Total ticks required to fully grow (adjusted by watering).</summary>
    public int GrowthTicksRequired;

    /// <summary>Current growth progress in ticks.</summary>
    public int GrowthTicksCurrent;

    /// <summary>Whether the crop has been watered this growth cycle.</summary>
    public bool IsWatered;

    /// <summary>Growth multiplier when watered (base 100 = 100%). E.g. 150 = 50% faster.</summary>
    public int WateredGrowthMultiplierBase100;

    /// <summary>Chance (0.0–1.0) to return a seed on harvest.</summary>
    public int SeedReturnChanceBase100;

    /// <summary>Current growth stage (0–3). Determines visual glyph.</summary>
    public readonly int GrowthStage
    {
        get
        {
            if (GrowthTicksRequired <= 0) return 3;
            float ratio = (float)GrowthTicksCurrent / GrowthTicksRequired;
            return ratio switch
            {
                >= 1.0f => 3,
                >= 0.66f => 2,
                >= 0.33f => 1,
                _ => 0,
            };
        }
    }

    public readonly bool IsFullyGrown => GrowthTicksRequired > 0 && GrowthTicksCurrent >= GrowthTicksRequired;
}
