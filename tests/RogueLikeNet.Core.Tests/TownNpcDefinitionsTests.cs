using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class TownNpcDefinitionsTests
{
    [Fact]
    public void Names_IsPopulated()
    {
        Assert.True(TownNpcDefinitions.Names.Length > 0);
    }

    [Fact]
    public void Dialogues_IsPopulated()
    {
        Assert.True(TownNpcDefinitions.Dialogues.Length > 0);
    }

    [Fact]
    public void PickName_ReturnsValidName()
    {
        var rng = new SeededRandom(42);
        var name = TownNpcDefinitions.PickName(rng);
        Assert.Contains(name, TownNpcDefinitions.Names);
    }

    [Fact]
    public void PickName_DifferentSeeds_ProduceDifferentNames()
    {
        // With enough different seeds, we should get at least 2 distinct names
        var names = Enumerable.Range(0, 20)
            .Select(i => TownNpcDefinitions.PickName(new SeededRandom(i)))
            .Distinct()
            .ToList();
        Assert.True(names.Count > 1);
    }

    [Fact]
    public void PickDialogue_ReturnsValidIndex()
    {
        var rng = new SeededRandom(42);
        int idx = TownNpcDefinitions.PickDialogue(rng);
        Assert.InRange(idx, 0, TownNpcDefinitions.Dialogues.Length - 1);
    }

    [Fact]
    public void PickDialogue_DifferentSeeds_ProduceDifferentIndices()
    {
        var indices = Enumerable.Range(0, 20)
            .Select(i => TownNpcDefinitions.PickDialogue(new SeededRandom(i)))
            .Distinct()
            .ToList();
        Assert.True(indices.Count > 1);
    }
}
