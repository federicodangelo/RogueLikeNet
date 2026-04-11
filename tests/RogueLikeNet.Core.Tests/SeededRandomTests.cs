using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class SeededRandomTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var rng1 = new SeededRandom(42);
        var rng2 = new SeededRandom(42);
        for (int i = 0; i < 100; i++)
            Assert.Equal(rng1.Next(), rng2.Next());
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSequences()
    {
        var rng1 = new SeededRandom(42);
        var rng2 = new SeededRandom(99);
        bool allSame = true;
        for (int i = 0; i < 10; i++)
            if (rng1.Next() != rng2.Next()) allSame = false;
        Assert.False(allSame);
    }

    [Fact]
    public void NextRange_RespectsMinMax()
    {
        var rng = new SeededRandom(123);
        for (int i = 0; i < 200; i++)
        {
            int val = rng.Next(5, 10);
            Assert.InRange(val, 5, 9);
        }
    }

    [Fact]
    public void Next_ProducesNonNegative()
    {
        var rng = new SeededRandom(1);
        for (int i = 0; i < 1000; i++)
            Assert.True(rng.Next() >= 0);
    }

    [Fact]
    public void Next_MaxExclusive_RespectsUpperBound()
    {
        var rng = new SeededRandom(42);
        for (int i = 0; i < 200; i++)
        {
            int val = rng.Next(5);
            Assert.InRange(val, 0, 4);
        }
    }

    [Fact]
    public void Next_MaxExclusive_ZeroOrNegative_ReturnsZero()
    {
        var rng = new SeededRandom(42);
        Assert.Equal(0, rng.Next(0));
        Assert.Equal(0, rng.Next(-1));
    }

    [Fact]
    public void NextRange_EqualMinMax_ReturnsMin()
    {
        var rng = new SeededRandom(42);
        Assert.Equal(5, rng.Next(5, 5));
    }

    [Fact]
    public void NextRange_MinGreaterThanMax_ReturnsMin()
    {
        var rng = new SeededRandom(42);
        Assert.Equal(10, rng.Next(10, 5));
    }

    [Fact]
    public void NextBool_ProducesBothValues()
    {
        var rng = new SeededRandom(42);
        bool seenTrue = false, seenFalse = false;
        for (int i = 0; i < 100; i++)
        {
            if (rng.NextBool()) seenTrue = true; else seenFalse = true;
            if (seenTrue && seenFalse) break;
        }
        Assert.True(seenTrue);
        Assert.True(seenFalse);
    }

    [Fact]
    public void Next_MaxExclusive_One_AlwaysReturnsZero()
    {
        var rng = new SeededRandom(42);
        for (int i = 0; i < 10; i++)
            Assert.Equal(0, rng.Next(1));
    }
}
