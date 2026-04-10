using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Core.Components;

public struct AnimalData
{
    /// <summary>Numeric ID of the animal definition.</summary>
    public int AnimalTypeId;

    /// <summary>Current tick counter towards next production.</summary>
    public int ProduceTicksCurrent;

    /// <summary>Whether the animal has been fed (enables production).</summary>
    public bool IsFed;

    /// <summary>Ticks remaining on the fed status before it expires.</summary>
    public int FedTicksRemaining;

    /// <summary>Current breed cooldown counter.</summary>
    public int BreedCooldownCurrent;

    public readonly bool CanProduce(AnimalDefinition def) => IsFed && ProduceTicksCurrent >= def.ProduceIntervalTicks;
    public readonly bool CanBreed => IsFed && BreedCooldownCurrent <= 0;
}
