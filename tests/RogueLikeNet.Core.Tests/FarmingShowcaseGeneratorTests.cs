using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class FarmingShowcaseGeneratorTests
{
    [Fact]
    public void Generate_SpawnChunk_HasSpawnPosition()
    {
        var gen = new FarmingShowcaseGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        Assert.NotNull(result.SpawnPosition);
        Assert.NotNull(result.Chunk);
    }

    [Fact]
    public void Generate_SpawnChunk_ContainsCrops()
    {
        var gen = new FarmingShowcaseGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        Assert.True(result.Chunk.Crops.Length > 0, "Expected crops in the farming showcase");
    }

    [Fact]
    public void Generate_SpawnChunk_ContainsAnimals()
    {
        var gen = new FarmingShowcaseGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        Assert.True(result.Animals.Count > 0, "Expected animals in the farming showcase");
    }

    [Fact]
    public void Generate_SpawnChunk_ContainsFarmingTools()
    {
        var gen = new FarmingShowcaseGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        var hoeId = GameData.Instance.Items.GetNumericId("wooden_hoe");
        var wateringCanId = GameData.Instance.Items.GetNumericId("watering_can");

        Assert.Contains(result.Items, i => i.Item.ItemTypeId == hoeId);
        Assert.Contains(result.Items, i => i.Item.ItemTypeId == wateringCanId);
    }

    [Fact]
    public void Generate_SpawnChunk_ContainsSeeds()
    {
        var gen = new FarmingShowcaseGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        var wheatSeedId = GameData.Instance.Items.GetNumericId("wheat_seeds");
        Assert.Contains(result.Items, i => i.Item.ItemTypeId == wheatSeedId);
    }

    [Fact]
    public void Generate_SpawnChunk_ContainsAnimalFeed()
    {
        var gen = new FarmingShowcaseGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        var feedId = GameData.Instance.Items.GetNumericId("animal_feed");
        Assert.Contains(result.Items, i => i.Item.ItemTypeId == feedId);
    }

    [Fact]
    public void Generate_SpawnChunk_HasTilledSoil()
    {
        var gen = new FarmingShowcaseGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        bool hasTilled = false;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (result.Chunk.Tiles[x, y].GlyphId == FarmingSystem.TilledSoilGlyphId)
                    hasTilled = true;

        Assert.True(hasTilled, "Expected tilled soil tiles in the farming showcase");
    }

    [Fact]
    public void Generate_SpawnChunk_HasCropsAtVariousStages()
    {
        var gen = new FarmingShowcaseGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        bool hasStage0 = false, hasStage3 = false;
        foreach (var crop in result.Chunk.Crops)
        {
            if (crop.CropData.GrowthStage == 0) hasStage0 = true;
            if (crop.CropData.GrowthStage == 3) hasStage3 = true;
        }

        Assert.True(hasStage0, "Expected seedling crops (stage 0)");
        Assert.True(hasStage3, "Expected mature crops (stage 3)");
    }

    [Fact]
    public void Generate_SpawnChunk_HasWateredCrops()
    {
        var gen = new FarmingShowcaseGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        bool hasWatered = false;
        foreach (var crop in result.Chunk.Crops)
            if (crop.CropData.IsWatered) hasWatered = true;

        Assert.True(hasWatered, "Expected watered crops in the showcase");
    }

    [Fact]
    public void Generate_SpawnChunk_HasAllAnimalTypes()
    {
        var gen = new FarmingShowcaseGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        var chickenDef = GameData.Instance.Animals.Get("chicken")!;
        var cowDef = GameData.Instance.Animals.Get("cow")!;
        var sheepDef = GameData.Instance.Animals.Get("sheep")!;

        Assert.Contains(result.Animals, a => a.AnimalDef.Id == chickenDef.Id);
        Assert.Contains(result.Animals, a => a.AnimalDef.Id == cowDef.Id);
        Assert.Contains(result.Animals, a => a.AnimalDef.Id == sheepDef.Id);
    }

    [Fact]
    public void Generate_NonSpawnChunk_IsEmptyGrass()
    {
        var gen = new FarmingShowcaseGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(1, 0, Position.DefaultZ));

        Assert.Null(result.SpawnPosition);
        Assert.Empty(result.Animals);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Chunk.Crops.Length);
    }

    [Fact]
    public void Generate_WrongZ_ReturnsEmpty()
    {
        var gen = new FarmingShowcaseGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ - 1));

        Assert.Null(result.SpawnPosition);
    }

    [Fact]
    public void FullIntegration_EngineLoadsChunkWithAnimals()
    {
        var gen = new FarmingShowcaseGenerator(42);
        using var engine = new GameEngine(42, gen);
        var chunk = engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        // Animals should be spawned in the chunk
        Assert.True(chunk.Animals.Length > 0, "Expected animals spawned by engine");
    }
}
