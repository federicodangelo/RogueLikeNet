using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class WorldMapTests
{
    private static TileInfo MakeClosedDoor() => new()
    {
        Type = TileType.Floor,
        GlyphId = TileDefinitions.GlyphFloor,
        FgColor = TileDefinitions.ColorFloorFg,
        BgColor = TileDefinitions.ColorBlack,
        PlaceableItemId = ItemDefinitions.WoodenDoor,
        PlaceableItemExtra = 0,
    };

    private static TileInfo MakeFloor() => new()
    {
        Type = TileType.Floor,
        GlyphId = TileDefinitions.GlyphFloor,
        FgColor = TileDefinitions.ColorFloorFg,
        BgColor = TileDefinitions.ColorBlack,
    };

    [Fact]
    public void GetOrCreateChunk_GeneratesOnDemand()
    {
        var map = new WorldMap(42);
        var gen = new BspDungeonGenerator(42);
        var (chunk, genResult) = map.GetOrCreateChunk(0, 0, Position.DefaultZ, gen);
        Assert.NotNull(chunk);
        Assert.Equal(0, chunk.ChunkX);
        Assert.Equal(0, chunk.ChunkY);
        Assert.NotNull(genResult);
    }

    [Fact]
    public void GetOrCreateChunk_ReturnsSameChunk()
    {
        var map = new WorldMap(42);
        var gen = new BspDungeonGenerator(42);
        var (c1, _) = map.GetOrCreateChunk(0, 0, Position.DefaultZ, gen);
        var (c2, genResult2) = map.GetOrCreateChunk(0, 0, Position.DefaultZ, gen);
        Assert.Same(c1, c2);
        Assert.Null(genResult2);
    }

    [Fact]
    public void TryGetChunk_ReturnsNullForMissing()
    {
        var map = new WorldMap(42);
        Assert.Null(map.TryGetChunk(0, 0, Position.DefaultZ));
    }

    [Fact]
    public void Seed_ReturnsConstructorValue()
    {
        var map = new WorldMap(12345);
        Assert.Equal(12345, map.Seed);
    }

    [Fact]
    public void GetTile_ReturnsDefaultForMissingChunk()
    {
        var map = new WorldMap(42);
        var tile = map.GetTile(0, 0, Position.DefaultZ);
        Assert.Equal(TileType.Void, tile.Type);
    }

    [Fact]
    public void GetTile_ReturnsCorrectTileForLoadedChunk()
    {
        var map = new WorldMap(42);
        var gen = new BspDungeonGenerator(42);
        map.GetOrCreateChunk(0, 0, Position.DefaultZ, gen);
        bool hasNonVoid = false;
        for (int x = 0; x < Chunk.Size && !hasNonVoid; x++)
            for (int y = 0; y < Chunk.Size && !hasNonVoid; y++)
            {
                var tile = map.GetTile(x, y, Position.DefaultZ);
                if (tile.Type != TileType.Void) hasNonVoid = true;
            }
        Assert.True(hasNonVoid);
    }

    [Fact]
    public void IsWalkable_ReturnsFalseForMissingChunk()
    {
        var map = new WorldMap(42);
        Assert.False(map.IsWalkable(0, 0, Position.DefaultZ));
    }

    // ──────────────────────────────────────────────
    // Dynamic tile tracking & door auto-close tests
    // ──────────────────────────────────────────────

    [Fact]
    public void OpenDoor_SetsExtraToGraceTicks()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(0, 0, Position.DefaultZ);
        chunk.Tiles[5, 5] = MakeClosedDoor();
        map.AddChunk(chunk);

        map.OpenDoor(5, 5, Position.DefaultZ);

        var tile = map.GetTile(5, 5, Position.DefaultZ);
        Assert.Equal(WorldMap.DoorGraceTicks, tile.PlaceableItemExtra);
        Assert.True(PlaceableDefinitions.IsDoorOpen(tile.PlaceableItemId, tile.PlaceableItemExtra));
    }

    [Fact]
    public void OpenDoor_TrackedAsDynamicTile()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(0, 0, Position.DefaultZ);
        chunk.Tiles[5, 5] = MakeClosedDoor();
        map.AddChunk(chunk);

        Assert.False(map.IsDynamicTileTracked(5, 5, Position.DefaultZ));

        map.OpenDoor(5, 5, Position.DefaultZ);

        Assert.True(map.IsDynamicTileTracked(5, 5, Position.DefaultZ));
    }

    [Fact]
    public void DoorClosesAfterExactGraceTicks()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(0, 0, Position.DefaultZ);
        chunk.Tiles[5, 5] = MakeClosedDoor();
        map.AddChunk(chunk);

        map.OpenDoor(5, 5, Position.DefaultZ);

        // Tick (GraceTicks - 1) times: door should still be open
        for (int i = 0; i < WorldMap.DoorGraceTicks - 1; i++)
            map.Update();

        var tile = map.GetTile(5, 5, Position.DefaultZ);
        Assert.True(PlaceableDefinitions.IsDoorOpen(tile.PlaceableItemId, tile.PlaceableItemExtra));
        Assert.Equal(1, tile.PlaceableItemExtra); // one tick remaining
        Assert.True(map.IsDynamicTileTracked(5, 5, Position.DefaultZ));

        // One more tick: door should close
        map.Update();

        tile = map.GetTile(5, 5, Position.DefaultZ);
        Assert.Equal(0, tile.PlaceableItemExtra);
        Assert.True(PlaceableDefinitions.IsDoorClosed(tile.PlaceableItemId, tile.PlaceableItemExtra));
        Assert.False(map.IsDynamicTileTracked(5, 5, Position.DefaultZ));
    }

    [Fact]
    public void DoorStaysOpenWhenOccupied()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(0, 0, Position.DefaultZ);
        chunk.Tiles[5, 5] = MakeClosedDoor();
        map.AddChunk(chunk);

        map.OpenDoor(5, 5, Position.DefaultZ);

        // Place a monster on the door tile to keep it occupied
        chunk.Monsters.Add(new MonsterEntity
        {
            Id = map.AllocateEntityId(),
            X = 5,
            Y = 5,
            Z = Position.DefaultZ,
            Health = new Health(10),
            CombatStats = default,
            MonsterData = new MonsterData { MonsterTypeId = 1 }
        });

        // Tick well past grace period
        for (int i = 0; i < WorldMap.DoorGraceTicks + 10; i++)
            map.Update();

        // Door should remain open because it's occupied
        var tile = map.GetTile(5, 5, Position.DefaultZ);
        Assert.True(PlaceableDefinitions.IsDoorOpen(tile.PlaceableItemId, tile.PlaceableItemExtra));
        Assert.True(map.IsDynamicTileTracked(5, 5, Position.DefaultZ));
    }

    [Fact]
    public void DoorClosesAfterEntityLeaves()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(0, 0, Position.DefaultZ);
        chunk.Tiles[5, 5] = MakeClosedDoor();
        map.AddChunk(chunk);

        map.OpenDoor(5, 5, Position.DefaultZ);

        var monster = new MonsterEntity
        {
            Id = map.AllocateEntityId(),
            X = 5,
            Y = 5,
            Z = Position.DefaultZ,
            Health = new Health(10),
            CombatStats = default,
            MonsterData = new MonsterData { MonsterTypeId = 1 }
        };
        chunk.Monsters.Add(monster);

        // Tick past grace period while occupied
        for (int i = 0; i < WorldMap.DoorGraceTicks + 5; i++)
            map.Update();

        // Door still open
        Assert.True(PlaceableDefinitions.IsDoorOpen(
            map.GetTile(5, 5, Position.DefaultZ).PlaceableItemId,
            map.GetTile(5, 5, Position.DefaultZ).PlaceableItemExtra));

        // Move entity away
        monster.X = 10;

        // One tick should close the door (timer is at 1 = minimum open, unoccupied -> close)
        map.Update();

        var tile = map.GetTile(5, 5, Position.DefaultZ);
        Assert.Equal(0, tile.PlaceableItemExtra);
        Assert.False(map.IsDynamicTileTracked(5, 5, Position.DefaultZ));
    }

    [Fact]
    public void SaveLoad_OpenDoor_PreservesStateAndAutoCloses()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(0, 0, Position.DefaultZ);
        chunk.Tiles[5, 5] = MakeClosedDoor();
        map.AddChunk(chunk);
        map.OpenDoor(5, 5, Position.DefaultZ);

        int ticksBeforeSave = 5;
        for (int i = 0; i < ticksBeforeSave; i++)
            map.Update();

        int remainingTicks = map.GetTile(5, 5, Position.DefaultZ).PlaceableItemExtra;
        Assert.Equal(WorldMap.DoorGraceTicks - ticksBeforeSave, remainingTicks);

        // Simulate save/load
        var newMap = new WorldMap(42);
        var newChunk = new Chunk(0, 0, Position.DefaultZ);
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                newChunk.Tiles[x, y] = chunk.Tiles[x, y];
        newMap.AddChunk(newChunk);

        var loadedTile = newMap.GetTile(5, 5, Position.DefaultZ);
        Assert.True(PlaceableDefinitions.IsDoorOpen(loadedTile.PlaceableItemId, loadedTile.PlaceableItemExtra));
        Assert.Equal(remainingTicks, loadedTile.PlaceableItemExtra);
        Assert.True(newMap.IsDynamicTileTracked(5, 5, Position.DefaultZ));

        for (int i = 0; i < remainingTicks; i++)
            newMap.Update();

        loadedTile = newMap.GetTile(5, 5, Position.DefaultZ);
        Assert.Equal(0, loadedTile.PlaceableItemExtra);
        Assert.True(PlaceableDefinitions.IsDoorClosed(loadedTile.PlaceableItemId, loadedTile.PlaceableItemExtra));
        Assert.False(newMap.IsDynamicTileTracked(5, 5, Position.DefaultZ));
    }

    [Fact]
    public void SetTile_TracksAndUntracksDynamicTiles()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(0, 0, Position.DefaultZ);
        chunk.Tiles[5, 5] = MakeFloor();
        map.AddChunk(chunk);

        Assert.False(map.IsDynamicTileTracked(5, 5, Position.DefaultZ));

        var openDoor = MakeClosedDoor();
        openDoor.PlaceableItemExtra = WorldMap.DoorGraceTicks;
        map.SetTile(5, 5, Position.DefaultZ, openDoor);
        Assert.True(map.IsDynamicTileTracked(5, 5, Position.DefaultZ));

        map.SetTile(5, 5, Position.DefaultZ, MakeFloor());
        Assert.False(map.IsDynamicTileTracked(5, 5, Position.DefaultZ));
    }

    [Fact]
    public void AddChunk_ScansExistingOpenDoors()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(0, 0, Position.DefaultZ);

        var openDoor = MakeClosedDoor();
        openDoor.PlaceableItemExtra = 10;
        chunk.Tiles[3, 7] = openDoor;
        chunk.Tiles[10, 20] = MakeClosedDoor();

        map.AddChunk(chunk);

        Assert.True(map.IsDynamicTileTracked(3, 7, Position.DefaultZ));
        Assert.False(map.IsDynamicTileTracked(10, 20, Position.DefaultZ));
    }

    [Fact]
    public void UnloadChunk_ClearsDynamicTileTracking()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(0, 0, Position.DefaultZ);
        chunk.Tiles[5, 5] = MakeClosedDoor();
        map.AddChunk(chunk);

        map.OpenDoor(5, 5, Position.DefaultZ);
        Assert.True(map.IsDynamicTileTracked(5, 5, Position.DefaultZ));

        map.UnloadChunk(0, 0, Position.DefaultZ);
        Assert.False(map.IsDynamicTileTracked(5, 5, Position.DefaultZ));
    }

    [Fact]
    public void MultipleDoors_IndependentTimers()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(0, 0, Position.DefaultZ);
        chunk.Tiles[5, 5] = MakeClosedDoor();
        chunk.Tiles[10, 10] = MakeClosedDoor();
        map.AddChunk(chunk);

        // Open first door
        map.OpenDoor(5, 5, Position.DefaultZ);

        // Tick 5 times then open second door
        for (int i = 0; i < 5; i++)
            map.Update();
        map.OpenDoor(10, 10, Position.DefaultZ);

        Assert.Equal(WorldMap.DoorGraceTicks - 5, map.GetTile(5, 5, Position.DefaultZ).PlaceableItemExtra);
        Assert.Equal(WorldMap.DoorGraceTicks, map.GetTile(10, 10, Position.DefaultZ).PlaceableItemExtra);

        // Tick until first door closes
        for (int i = 0; i < WorldMap.DoorGraceTicks - 5; i++)
            map.Update();

        Assert.Equal(0, map.GetTile(5, 5, Position.DefaultZ).PlaceableItemExtra);
        Assert.Equal(5, map.GetTile(10, 10, Position.DefaultZ).PlaceableItemExtra);
        Assert.False(map.IsDynamicTileTracked(5, 5, Position.DefaultZ));
        Assert.True(map.IsDynamicTileTracked(10, 10, Position.DefaultZ));
    }

    // ──────────────────────────────────────────────
    // Entity migration marks chunks dirty
    // ──────────────────────────────────────────────

    [Fact]
    public void MigrateMonster_MarksBothChunksDirty()
    {
        var map = new WorldMap(42);
        var chunkA = new Chunk(0, 0, Position.DefaultZ);
        var chunkB = new Chunk(1, 0, Position.DefaultZ);
        map.AddChunk(chunkA);
        map.AddChunk(chunkB);

        // Place monster in chunk A
        int xA = 5;
        var monster = new MonsterEntity
        {
            Id = map.AllocateEntityId(),
            X = xA,
            Y = 5,
            Z = Position.DefaultZ,
            Health = new Health(10),
            CombatStats = default,
            MonsterData = new MonsterData { MonsterTypeId = 1 },
        };
        chunkA.Monsters.Add(monster);

        // Clear save flags
        chunkA.ClearSaveFlag();
        chunkB.ClearSaveFlag();
        Assert.False(chunkA.IsModifiedSinceLastSave);
        Assert.False(chunkB.IsModifiedSinceLastSave);

        // Move monster across chunk boundary
        map.MoveMonsterEntity(monster, Chunk.Size + 3, 5, Position.DefaultZ);

        Assert.True(chunkA.IsModifiedSinceLastSave);
        Assert.True(chunkB.IsModifiedSinceLastSave);
        Assert.DoesNotContain(monster, chunkA.Monsters);
        Assert.Contains(monster, chunkB.Monsters);
    }

    [Fact]
    public void MigrateNpc_MarksBothChunksDirty()
    {
        var map = new WorldMap(42);
        var chunkA = new Chunk(0, 0, Position.DefaultZ);
        var chunkB = new Chunk(1, 0, Position.DefaultZ);
        map.AddChunk(chunkA);
        map.AddChunk(chunkB);

        var npc = new TownNpcEntity
        {
            Id = map.AllocateEntityId(),
            X = 5,
            Y = 5,
            Z = Position.DefaultZ,
            Health = new Health(100),
            CombatStats = default,
            NpcData = new TownNpcTag { Name = "Test" },
        };
        chunkA.TownNpcs.Add(npc);

        chunkA.ClearSaveFlag();
        chunkB.ClearSaveFlag();

        map.MoveNpcEntity(npc, Chunk.Size + 3, 5, Position.DefaultZ);

        Assert.True(chunkA.IsModifiedSinceLastSave);
        Assert.True(chunkB.IsModifiedSinceLastSave);
        Assert.DoesNotContain(npc, chunkA.TownNpcs);
        Assert.Contains(npc, chunkB.TownNpcs);
    }

    [Fact]
    public void MigrateMonster_SameChunk_DoesNotDirty()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(0, 0, Position.DefaultZ);
        map.AddChunk(chunk);

        var monster = new MonsterEntity
        {
            Id = map.AllocateEntityId(),
            X = 5,
            Y = 5,
            Z = Position.DefaultZ,
            Health = new Health(10),
            CombatStats = default,
            MonsterData = new MonsterData { MonsterTypeId = 1 },
        };
        chunk.Monsters.Add(monster);
        chunk.ClearSaveFlag();

        // Move within same chunk
        map.MoveMonsterEntity(monster, 6, 5, Position.DefaultZ);

        Assert.False(chunk.IsModifiedSinceLastSave);
    }
}
