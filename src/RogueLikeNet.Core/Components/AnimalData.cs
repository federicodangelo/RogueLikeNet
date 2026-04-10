namespace RogueLikeNet.Core.Components;

public struct AnimalData
{
    /// <summary>Numeric ID of the animal definition.</summary>
    public int AnimalTypeId;

    /// <summary>String ID of the item this animal produces (e.g. "egg", "milk", "wool").</summary>
    public int ProduceItemTypeId;

    /// <summary>Ticks between each resource production.</summary>
    public int ProduceIntervalTicks;

    /// <summary>Current tick counter towards next production.</summary>
    public int ProduceTicksCurrent;

    /// <summary>Whether the animal has been fed (enables production).</summary>
    public bool IsFed;

    /// <summary>Ticks remaining on the fed status before it expires.</summary>
    public int FedTicksRemaining;

    /// <summary>Ticks required between breeding attempts.</summary>
    public int BreedCooldownTicks;

    /// <summary>Current breed cooldown counter.</summary>
    public int BreedCooldownCurrent;

    public readonly bool CanProduce => IsFed && ProduceTicksCurrent >= ProduceIntervalTicks;
    public readonly bool CanBreed => IsFed && BreedCooldownCurrent <= 0;
}
