namespace RogueLikeNet.Core.Components;

public struct Survival
{
    public const int MaxHunger = 100;
    public const int DefaultDecayRate = 20 * 30; // ticks per hunger point lost

    public int Hunger;
    public int HungerDecayRate;
    internal int DecayCounter;

    public Survival(int hunger, int decayRate)
    {
        Hunger = hunger;
        HungerDecayRate = decayRate;
        DecayCounter = 0;
    }

    public static Survival Default() => new(MaxHunger, DefaultDecayRate);

    public readonly bool IsStarving => Hunger < 20;
    public readonly bool IsHungry => Hunger < 50;
}
