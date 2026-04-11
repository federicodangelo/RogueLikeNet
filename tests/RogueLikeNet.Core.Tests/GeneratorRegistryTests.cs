using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class GeneratorRegistryTests
{
    [Fact]
    public void Count_ReturnsAllEntries()
    {
        Assert.True(GeneratorRegistry.Count > 0);
        Assert.Equal(GeneratorRegistry.All.Length, GeneratorRegistry.Count);
    }

    [Fact]
    public void DefaultIndex_IsZero()
    {
        Assert.Equal(0, GeneratorRegistry.DefaultIndex);
    }

    [Fact]
    public void DefaultId_IsOverworld()
    {
        Assert.Equal("overworld", GeneratorRegistry.DefaultId);
    }

    [Fact]
    public void Create_ByValidIndex_ReturnsGenerator()
    {
        var gen = GeneratorRegistry.Create(0, 42);
        Assert.NotNull(gen);
        Assert.IsType<OverworldGenerator>(gen);
    }

    [Fact]
    public void Create_ByNegativeIndex_FallsBackToDefault()
    {
        var gen = GeneratorRegistry.Create(-1, 42);
        Assert.NotNull(gen);
        Assert.IsType<OverworldGenerator>(gen);
    }

    [Fact]
    public void Create_ByOutOfRangeIndex_FallsBackToDefault()
    {
        var gen = GeneratorRegistry.Create(9999, 42);
        Assert.NotNull(gen);
        Assert.IsType<OverworldGenerator>(gen);
    }

    [Fact]
    public void Create_ByValidStringId_ReturnsCorrectGenerator()
    {
        var gen = GeneratorRegistry.Create("bsp-dungeon", 42);
        Assert.IsType<BspDungeonGenerator>(gen);
    }

    [Fact]
    public void Create_ByUnknownStringId_FallsBackToDefault()
    {
        var gen = GeneratorRegistry.Create("nonexistent", 42);
        Assert.IsType<OverworldGenerator>(gen);
    }

    [Fact]
    public void Create_ByCaseInsensitiveId_Works()
    {
        var gen = GeneratorRegistry.Create("BSP-DUNGEON", 42);
        Assert.IsType<BspDungeonGenerator>(gen);
    }

    [Fact]
    public void GetName_ValidIndex_ReturnsDisplayName()
    {
        Assert.Equal("Overworld", GeneratorRegistry.GetName(0));
    }

    [Fact]
    public void GetName_OutOfRange_ReturnsDefault()
    {
        Assert.Equal("Overworld", GeneratorRegistry.GetName(-1));
        Assert.Equal("Overworld", GeneratorRegistry.GetName(9999));
    }

    [Fact]
    public void GetId_ValidIndex_ReturnsStringId()
    {
        Assert.Equal("overworld", GeneratorRegistry.GetId(0));
    }

    [Fact]
    public void GetId_OutOfRange_ReturnsDefault()
    {
        Assert.Equal("overworld", GeneratorRegistry.GetId(-1));
        Assert.Equal("overworld", GeneratorRegistry.GetId(9999));
    }

    [Fact]
    public void GetIndex_ValidId_ReturnsCorrectIndex()
    {
        int idx = GeneratorRegistry.GetIndex("bsp-dungeon");
        Assert.Equal("bsp-dungeon", GeneratorRegistry.All[idx].Id);
    }

    [Fact]
    public void GetIndex_UnknownId_ReturnsDefaultIndex()
    {
        Assert.Equal(GeneratorRegistry.DefaultIndex, GeneratorRegistry.GetIndex("nonexistent"));
    }

    [Fact]
    public void GetNameOrId_ValidId_ReturnsDisplayName()
    {
        Assert.Equal("BSP Dungeon", GeneratorRegistry.GetNameOrId("bsp-dungeon"));
    }

    [Fact]
    public void GetNameOrId_UnknownId_ReturnsRawId()
    {
        Assert.Equal("unknown-gen", GeneratorRegistry.GetNameOrId("unknown-gen"));
    }

    [Fact]
    public void AllEntries_HaveUniqueIds()
    {
        var ids = GeneratorRegistry.All.Select(e => e.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void AllEntries_CanCreateGenerators()
    {
        for (int i = 0; i < GeneratorRegistry.Count; i++)
        {
            var gen = GeneratorRegistry.Create(i, 42);
            Assert.NotNull(gen);
        }
    }
}
