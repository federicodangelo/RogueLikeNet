using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Core.Components;

public struct CropData
{
    /// <summary>Numeric ID of the seed item that was planted. Used to look up SeedData from the definition.</summary>
    public int SeedItemTypeId;

    /// <summary>Current growth progress in ticks.</summary>
    public int GrowthTicksCurrent;

    /// <summary>Whether the crop has been watered this growth cycle.</summary>
    public bool IsWatered;

    /// <summary>Current growth stage (0–3). Requires the total growth ticks from the seed definition.</summary>
    public readonly int GetGrowthStage(SeedData seedData)
    {
        if (seedData.GrowthTicks <= 0) return 3;
        float ratio = (float)GrowthTicksCurrent / seedData.GrowthTicks;
        return ratio switch
        {
            >= 1.0f => 3,
            >= 0.66f => 2,
            >= 0.33f => 1,
            _ => 0,
        };
    }

    public readonly bool IsFullyGrown(SeedData seedData) => seedData.GrowthTicks > 0 && GrowthTicksCurrent >= seedData.GrowthTicks;
}
