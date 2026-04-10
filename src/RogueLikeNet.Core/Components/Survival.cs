namespace RogueLikeNet.Core.Components;

public struct Survival
{
    public const int DefaultMaxHunger = 100;
    public const int DefaultMaxThirst = 100;
    public const int DefaultDecayRate = 20 * 30; // ticks per hunger point lost (about 30 seconds at 20 TPS)
    public const int DefaultThirstDecayRate = 20 * 20; // ticks per thirst point lost (faster than hunger, about 20 seconds at 20 TPS)

    public int MaxHunger;
    public int Hunger;
    public int HungerDecayRate;
    internal int HungerDecayCounter;

    public int MaxThirst;
    public int Thirst;
    public int ThirstDecayRate;
    internal int ThirstDecayCounter;

    public Survival(int hunger, int hungerDecayRate, int thirst, int thirstDecayRate)
    {
        MaxHunger = DefaultMaxHunger;
        Hunger = hunger;
        HungerDecayRate = hungerDecayRate;
        HungerDecayCounter = 0;

        MaxThirst = DefaultMaxThirst;
        Thirst = thirst;
        ThirstDecayRate = thirstDecayRate;
        ThirstDecayCounter = 0;
    }

    public static Survival Default() => new(DefaultMaxHunger, DefaultDecayRate, DefaultMaxThirst, DefaultThirstDecayRate);

    public readonly bool IsStarving => Hunger < 20;
    public readonly bool IsHungry => Hunger < 50;
    public readonly bool IsWellFed => Hunger > 80;

    public readonly bool IsDehydrated => Thirst < 20;
    public readonly bool IsThirsty => Thirst < 50;
    public readonly bool IsWellHydrated => Thirst > 80;
}
