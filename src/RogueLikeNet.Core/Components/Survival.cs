namespace RogueLikeNet.Core.Components;

public struct Survival
{
    public const int DefaultMaxHunger = 100;
    public const int DefaultDecayRate = 20 * 30; // ticks per hunger point lost

    public int MaxHunger;
    public int Hunger;
    public int HungerDecayRate;
    internal int DecayCounter;

    public Survival(int hunger, int decayRate)
    {
        MaxHunger = DefaultMaxHunger; ;
        Hunger = hunger;
        HungerDecayRate = decayRate;
        DecayCounter = 0;
    }

    public static Survival Default() => new(DefaultMaxHunger, DefaultDecayRate);

    public readonly bool IsStarving => Hunger < 20;
    public readonly bool IsHungry => Hunger < 50;
}
