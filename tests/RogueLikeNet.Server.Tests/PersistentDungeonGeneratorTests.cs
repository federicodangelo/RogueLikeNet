using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Server.Tests;

public class PersistentDungeonGeneratorTests
{
    private sealed class FakeDungeonGenerator : IDungeonGenerator
    {
        public bool ExistsResult { get; set; } = true;
        public int GenerateCalls { get; private set; }

        public bool Exists(ChunkPosition chunkPos) => ExistsResult;

        public GenerationResult Generate(ChunkPosition chunkPos)
        {
            GenerateCalls++;
            var chunk = new Chunk(chunkPos);
            // Fill chunk with floor tiles
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                    chunk.Tiles[x, y] = new TileInfo { TileId = GameData.Instance.Tiles.GetNumericId("floor") };
            return new GenerationResult(chunk);
        }
    }

    private static ChunkPosition TestPos => ChunkPosition.FromCoords(0, 0, 127);

    [Fact]
    public void Exists_NoSavedChunk_DelegatesToBase_True()
    {
        using var provider = new InMemorySaveGameProvider();
        var slot = provider.CreateSaveSlot("test", 42, "bsp");
        var baseGen = new FakeDungeonGenerator { ExistsResult = true };
        var gen = new PersistentDungeonGenerator(baseGen, provider, slot.SlotId);

        Assert.True(gen.Exists(TestPos));
    }

    [Fact]
    public void Exists_NoSavedChunk_DelegatesToBase_False()
    {
        using var provider = new InMemorySaveGameProvider();
        var slot = provider.CreateSaveSlot("test", 42, "bsp");
        var baseGen = new FakeDungeonGenerator { ExistsResult = false };
        var gen = new PersistentDungeonGenerator(baseGen, provider, slot.SlotId);

        Assert.False(gen.Exists(TestPos));
    }

    [Fact]
    public void Exists_WithSavedChunk_ReturnsTrue()
    {
        using var provider = new InMemorySaveGameProvider();
        var slot = provider.CreateSaveSlot("test", 42, "bsp");

        // Save a chunk
        var pos = TestPos;
        var tiles = new TileInfo[Chunk.Size, Chunk.Size];
        tiles[0, 0] = new TileInfo { TileId = GameData.Instance.Tiles.GetNumericId("floor") };
        var tileData = ChunkSerializer.SerializeTiles(tiles);
        provider.SaveChunks(slot.SlotId, [new ChunkSaveEntry
        {
            ChunkX = pos.X, ChunkY = pos.Y, ChunkZ = pos.Z,
            TileData = tileData, EntityData = "[]"
        }]);

        var baseGen = new FakeDungeonGenerator { ExistsResult = false };
        var gen = new PersistentDungeonGenerator(baseGen, provider, slot.SlotId);

        Assert.True(gen.Exists(pos)); // Should return true from saved data
    }

    [Fact]
    public void Generate_NoSavedChunk_DelegatesToBase()
    {
        using var provider = new InMemorySaveGameProvider();
        var slot = provider.CreateSaveSlot("test", 42, "bsp");
        var baseGen = new FakeDungeonGenerator();
        var gen = new PersistentDungeonGenerator(baseGen, provider, slot.SlotId);

        var result = gen.Generate(TestPos);

        Assert.Equal(1, baseGen.GenerateCalls);
        Assert.NotNull(result.Chunk);
    }

    [Fact]
    public void Generate_WithSavedChunk_RestoresTiles()
    {
        using var provider = new InMemorySaveGameProvider();
        var slot = provider.CreateSaveSlot("test", 42, "bsp");

        // Create tiles with specific pattern
        var pos = TestPos;
        var tiles = new TileInfo[Chunk.Size, Chunk.Size];
        tiles[0, 0] = new TileInfo { TileId = GameData.Instance.Tiles.GetNumericId("floor") };
        tiles[1, 1] = new TileInfo { TileId = GameData.Instance.Tiles.GetNumericId("stairs_down") };
        var tileData = ChunkSerializer.SerializeTiles(tiles);
        provider.SaveChunks(slot.SlotId, [new ChunkSaveEntry
        {
            ChunkX = pos.X, ChunkY = pos.Y, ChunkZ = pos.Z,
            TileData = tileData, EntityData = "[]"
        }]);

        var baseGen = new FakeDungeonGenerator();
        var gen = new PersistentDungeonGenerator(baseGen, provider, slot.SlotId);

        var result = gen.Generate(pos);

        Assert.Equal(0, baseGen.GenerateCalls); // Should NOT delegate to base
        Assert.Equal(TileType.Floor, result.Chunk.Tiles[0, 0].Type);
        Assert.Equal(TileType.StairsDown, result.Chunk.Tiles[1, 1].Type);
    }

    [Fact]
    public void Generate_WithSavedChunk_HasEntityData_SetsRawEntityJson()
    {
        using var provider = new InMemorySaveGameProvider();
        var slot = provider.CreateSaveSlot("test", 42, "bsp");

        var pos = TestPos;
        var tiles = new TileInfo[Chunk.Size, Chunk.Size];
        var tileData = ChunkSerializer.SerializeTiles(tiles);
        string entityJson = """[{"type":"monster","x":5,"y":5}]""";
        provider.SaveChunks(slot.SlotId, [new ChunkSaveEntry
        {
            ChunkX = pos.X, ChunkY = pos.Y, ChunkZ = pos.Z,
            TileData = tileData, EntityData = entityJson
        }]);

        var baseGen = new FakeDungeonGenerator();
        var gen = new PersistentDungeonGenerator(baseGen, provider, slot.SlotId);

        var result = gen.Generate(pos);

        Assert.Equal(entityJson, result.RawEntityJson);
    }

    [Fact]
    public void Generate_WithSavedChunk_EmptyEntityData_NoRawEntityJson()
    {
        using var provider = new InMemorySaveGameProvider();
        var slot = provider.CreateSaveSlot("test", 42, "bsp");

        var pos = TestPos;
        var tiles = new TileInfo[Chunk.Size, Chunk.Size];
        var tileData = ChunkSerializer.SerializeTiles(tiles);
        provider.SaveChunks(slot.SlotId, [new ChunkSaveEntry
        {
            ChunkX = pos.X, ChunkY = pos.Y, ChunkZ = pos.Z,
            TileData = tileData, EntityData = "[]"
        }]);

        var baseGen = new FakeDungeonGenerator();
        var gen = new PersistentDungeonGenerator(baseGen, provider, slot.SlotId);

        var result = gen.Generate(pos);

        Assert.Null(result.RawEntityJson); // "[]" should be treated as empty
    }
}
