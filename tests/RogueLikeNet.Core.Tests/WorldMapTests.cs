using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;
using RogueLikeNet.Core.Entities;

namespace RogueLikeNet.Core.Tests;

public class WorldMapTests
{
    private static TileInfo MakeClosedDoor() => new()
    {
        Type = TileType.Floor,
        GlyphId = TileDefinitions.GlyphFloor,
        FgColor = TileDefinitions.ColorFloorFg,
        BgColor = TileDefinitions.ColorBlack,
        PlaceableItemId = GameData.Instance.Items.GetNumericId("wooden_door"),
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
        var (chunk, genResult) = map.GetOrCreateChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ), gen);
        Assert.NotNull(chunk);
        Assert.Equal(0, chunk.ChunkPosition.X);
        Assert.Equal(0, chunk.ChunkPosition.Y);
        Assert.NotNull(genResult);
    }

    [Fact]
    public void GetOrCreateChunk_ReturnsSameChunk()
    {
        var map = new WorldMap(42);
        var gen = new BspDungeonGenerator(42);
        var (c1, _) = map.GetOrCreateChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ), gen);
        var (c2, genResult2) = map.GetOrCreateChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ), gen);
        Assert.Same(c1, c2);
        Assert.Null(genResult2);
    }

    [Fact]
    public void TryGetChunk_ReturnsNullForMissing()
    {
        var map = new WorldMap(42);
        Assert.Null(map.TryGetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ)));
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
        var tile = map.GetTile(Position.FromCoords(0, 0, Position.DefaultZ));
        Assert.Equal(TileType.Void, tile.Type);
    }

    [Fact]
    public void GetTile_ReturnsCorrectTileForLoadedChunk()
    {
        var map = new WorldMap(42);
        var gen = new BspDungeonGenerator(42);
        map.GetOrCreateChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ), gen);
        bool hasNonVoid = false;
        for (int x = 0; x < Chunk.Size && !hasNonVoid; x++)
            for (int y = 0; y < Chunk.Size && !hasNonVoid; y++)
            {
                var tile = map.GetTile(Position.FromCoords(x, y, Position.DefaultZ));
                if (tile.Type != TileType.Void) hasNonVoid = true;
            }
        Assert.True(hasNonVoid);
    }

    [Fact]
    public void IsWalkable_ReturnsFalseForMissingChunk()
    {
        var map = new WorldMap(42);
        Assert.False(map.IsWalkable(Position.FromCoords(0, 0, Position.DefaultZ)));
    }

    // ──────────────────────────────────────────────
    // Dynamic tile tracking & door auto-close tests
    // ──────────────────────────────────────────────

    [Fact]
    public void OpenDoor_SetsExtraToGraceTicks()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.Tiles[5, 5] = MakeClosedDoor();
        map.AddChunk(chunk);

        map.OpenDoor(Position.FromCoords(5, 5, Position.DefaultZ));

        var tile = map.GetTile(Position.FromCoords(5, 5, Position.DefaultZ));
        Assert.Equal(WorldMap.DoorGraceTicks, tile.PlaceableItemExtra);
        Assert.True(GameData.Instance.Items.IsPlaceableDoorOpen(tile.PlaceableItemId, tile.PlaceableItemExtra));
    }

    [Fact]
    public void OpenDoor_TrackedAsDynamicTile()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.Tiles[5, 5] = MakeClosedDoor();
        map.AddChunk(chunk);

        Assert.False(map.IsDynamicTileTracked(Position.FromCoords(5, 5, Position.DefaultZ)));

        map.OpenDoor(Position.FromCoords(5, 5, Position.DefaultZ));

        Assert.True(map.IsDynamicTileTracked(Position.FromCoords(5, 5, Position.DefaultZ)));
    }

    [Fact]
    public void DoorClosesAfterExactGraceTicks()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.Tiles[5, 5] = MakeClosedDoor();
        map.AddChunk(chunk);

        map.OpenDoor(Position.FromCoords(5, 5, Position.DefaultZ));

        // Tick (GraceTicks - 1) times: door should still be open
        for (int i = 0; i < WorldMap.DoorGraceTicks - 1; i++)
            map.Update();

        var tile = map.GetTile(Position.FromCoords(5, 5, Position.DefaultZ));
        Assert.True(GameData.Instance.Items.IsPlaceableDoorOpen(tile.PlaceableItemId, tile.PlaceableItemExtra));
        Assert.Equal(1, tile.PlaceableItemExtra); // one tick remaining
        Assert.True(map.IsDynamicTileTracked(Position.FromCoords(5, 5, Position.DefaultZ)));

        // One more tick: door should close
        map.Update();

        tile = map.GetTile(Position.FromCoords(5, 5, Position.DefaultZ));
        Assert.Equal(0, tile.PlaceableItemExtra);
        Assert.True(GameData.Instance.Items.IsPlaceableDoorClosed(tile.PlaceableItemId, tile.PlaceableItemExtra));
        Assert.False(map.IsDynamicTileTracked(Position.FromCoords(5, 5, Position.DefaultZ)));
    }

    [Fact]
    public void DoorStaysOpenWhenOccupied()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.Tiles[5, 5] = MakeClosedDoor();
        map.AddChunk(chunk);

        map.OpenDoor(Position.FromCoords(5, 5, Position.DefaultZ));

        // Place a monster on the door tile to keep it occupied
        chunk.AddEntity(new MonsterEntity(map.AllocateEntityId())
        {
            Position = Position.FromCoords(5, 5, Position.DefaultZ),
            Health = new Health(10),
            CombatStats = default,
            MonsterData = new MonsterData { MonsterTypeId = 1 }
        });

        // Tick well past grace period
        for (int i = 0; i < WorldMap.DoorGraceTicks + 10; i++)
            map.Update();

        // Door should remain open because it's occupied
        var tile = map.GetTile(Position.FromCoords(5, 5, Position.DefaultZ));
        Assert.True(GameData.Instance.Items.IsPlaceableDoorOpen(tile.PlaceableItemId, tile.PlaceableItemExtra));
        Assert.True(map.IsDynamicTileTracked(Position.FromCoords(5, 5, Position.DefaultZ)));
    }

    [Fact]
    public void DoorClosesAfterEntityLeaves()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.Tiles[5, 5] = MakeClosedDoor();
        map.AddChunk(chunk);

        map.OpenDoor(Position.FromCoords(5, 5, Position.DefaultZ));

        var monster = new MonsterEntity(map.AllocateEntityId())
        {
            Position = Position.FromCoords(5, 5, Position.DefaultZ),
            Health = new Health(10),
            CombatStats = default,
            MonsterData = new MonsterData { MonsterTypeId = 1 }
        };
        chunk.AddEntity(monster);

        // Tick past grace period while occupied
        for (int i = 0; i < WorldMap.DoorGraceTicks + 5; i++)
            map.Update();

        // Door still open
        Assert.True(GameData.Instance.Items.IsPlaceableDoorOpen(
            map.GetTile(Position.FromCoords(5, 5, Position.DefaultZ)).PlaceableItemId,
            map.GetTile(Position.FromCoords(5, 5, Position.DefaultZ)).PlaceableItemExtra));

        // Move entity away
        ref var monsterRef = ref chunk.GetMonsterRef(monster.Id);
        monsterRef.Position = Position.FromCoords(10, monsterRef.Position.Y, monsterRef.Position.Z);

        // One tick should close the door (timer is at 1 = minimum open, unoccupied -> close)
        map.Update();

        var tile = map.GetTile(Position.FromCoords(5, 5, Position.DefaultZ));
        Assert.Equal(0, tile.PlaceableItemExtra);
        Assert.False(map.IsDynamicTileTracked(Position.FromCoords(5, 5, Position.DefaultZ)));
    }

    [Fact]
    public void SaveLoad_OpenDoor_PreservesStateAndAutoCloses()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.Tiles[5, 5] = MakeClosedDoor();
        map.AddChunk(chunk);
        map.OpenDoor(Position.FromCoords(5, 5, Position.DefaultZ));

        int ticksBeforeSave = 5;
        for (int i = 0; i < ticksBeforeSave; i++)
            map.Update();

        int remainingTicks = map.GetTile(Position.FromCoords(5, 5, Position.DefaultZ)).PlaceableItemExtra;
        Assert.Equal(WorldMap.DoorGraceTicks - ticksBeforeSave, remainingTicks);

        // Simulate save/load
        var newMap = new WorldMap(42);
        var newChunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                newChunk.Tiles[x, y] = chunk.Tiles[x, y];
        newMap.AddChunk(newChunk);

        var loadedTile = newMap.GetTile(Position.FromCoords(5, 5, Position.DefaultZ));
        Assert.True(GameData.Instance.Items.IsPlaceableDoorOpen(loadedTile.PlaceableItemId, loadedTile.PlaceableItemExtra));
        Assert.Equal(remainingTicks, loadedTile.PlaceableItemExtra);
        Assert.True(newMap.IsDynamicTileTracked(Position.FromCoords(5, 5, Position.DefaultZ)));

        for (int i = 0; i < remainingTicks; i++)
            newMap.Update();

        loadedTile = newMap.GetTile(Position.FromCoords(5, 5, Position.DefaultZ));
        Assert.Equal(0, loadedTile.PlaceableItemExtra);
        Assert.True(GameData.Instance.Items.IsPlaceableDoorClosed(loadedTile.PlaceableItemId, loadedTile.PlaceableItemExtra));
        Assert.False(newMap.IsDynamicTileTracked(Position.FromCoords(5, 5, Position.DefaultZ)));
    }

    [Fact]
    public void SetTile_TracksAndUntracksDynamicTiles()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.Tiles[5, 5] = MakeFloor();
        map.AddChunk(chunk);

        Assert.False(map.IsDynamicTileTracked(Position.FromCoords(5, 5, Position.DefaultZ)));

        var openDoor = MakeClosedDoor();
        openDoor.PlaceableItemExtra = WorldMap.DoorGraceTicks;
        map.SetTile(Position.FromCoords(5, 5, Position.DefaultZ), openDoor);
        Assert.True(map.IsDynamicTileTracked(Position.FromCoords(5, 5, Position.DefaultZ)));

        map.SetTile(Position.FromCoords(5, 5, Position.DefaultZ), MakeFloor());
        Assert.False(map.IsDynamicTileTracked(Position.FromCoords(5, 5, Position.DefaultZ)));
    }

    [Fact]
    public void AddChunk_ScansExistingOpenDoors()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        var openDoor = MakeClosedDoor();
        openDoor.PlaceableItemExtra = 10;
        chunk.Tiles[3, 7] = openDoor;
        chunk.Tiles[10, 20] = MakeClosedDoor();

        map.AddChunk(chunk);

        Assert.True(map.IsDynamicTileTracked(Position.FromCoords(3, 7, Position.DefaultZ)));
        Assert.False(map.IsDynamicTileTracked(Position.FromCoords(10, 20, Position.DefaultZ)));
    }

    [Fact]
    public void UnloadChunk_ClearsDynamicTileTracking()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.Tiles[5, 5] = MakeClosedDoor();
        map.AddChunk(chunk);

        map.OpenDoor(Position.FromCoords(5, 5, Position.DefaultZ));
        Assert.True(map.IsDynamicTileTracked(Position.FromCoords(5, 5, Position.DefaultZ)));

        map.UnloadChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        Assert.False(map.IsDynamicTileTracked(Position.FromCoords(5, 5, Position.DefaultZ)));
    }

    [Fact]
    public void MultipleDoors_IndependentTimers()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.Tiles[5, 5] = MakeClosedDoor();
        chunk.Tiles[10, 10] = MakeClosedDoor();
        map.AddChunk(chunk);

        // Open first door
        map.OpenDoor(Position.FromCoords(5, 5, Position.DefaultZ));

        // Tick 5 times then open second door
        for (int i = 0; i < 5; i++)
            map.Update();
        map.OpenDoor(Position.FromCoords(10, 10, Position.DefaultZ));

        Assert.Equal(WorldMap.DoorGraceTicks - 5, map.GetTile(Position.FromCoords(5, 5, Position.DefaultZ)).PlaceableItemExtra);
        Assert.Equal(WorldMap.DoorGraceTicks, map.GetTile(Position.FromCoords(10, 10, Position.DefaultZ)).PlaceableItemExtra);

        // Tick until first door closes
        for (int i = 0; i < WorldMap.DoorGraceTicks - 5; i++)
            map.Update();

        Assert.Equal(0, map.GetTile(Position.FromCoords(5, 5, Position.DefaultZ)).PlaceableItemExtra);
        Assert.Equal(5, map.GetTile(Position.FromCoords(10, 10, Position.DefaultZ)).PlaceableItemExtra);
        Assert.False(map.IsDynamicTileTracked(Position.FromCoords(5, 5, Position.DefaultZ)));
        Assert.True(map.IsDynamicTileTracked(Position.FromCoords(10, 10, Position.DefaultZ)));
    }

    // ──────────────────────────────────────────────
    // Entity migration marks chunks dirty
    // ──────────────────────────────────────────────

    [Fact]
    public void MigrateMonster_MarksBothChunksDirty()
    {
        var map = new WorldMap(42);
        var chunkA = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var chunkB = new Chunk(ChunkPosition.FromCoords(1, 0, Position.DefaultZ));
        map.AddChunk(chunkA);
        map.AddChunk(chunkB);

        // Place monster in chunk A
        int xA = 5;
        var monster = new MonsterEntity(map.AllocateEntityId())
        {
            Position = Position.FromCoords(xA, 5, Position.DefaultZ),
            Health = new Health(10),
            CombatStats = default,
            MonsterData = new MonsterData { MonsterTypeId = 1 },
        };
        chunkA.AddEntity(monster);

        // Clear save flags
        chunkA.ClearSaveFlag();
        chunkB.ClearSaveFlag();
        Assert.False(chunkA.IsModifiedSinceLastSave);
        Assert.False(chunkB.IsModifiedSinceLastSave);

        // Move monster across chunk boundary
        map.MoveMonsterEntity(monster.Id, monster.Position, Position.FromCoords(Chunk.Size + 3, 5, Position.DefaultZ));

        Assert.True(chunkA.IsModifiedSinceLastSave);
        Assert.True(chunkB.IsModifiedSinceLastSave);
        Assert.Empty(chunkA.Monsters.ToArray());
        Assert.Single(chunkB.Monsters.ToArray());
        Assert.Equal(monster.Id, chunkB.Monsters[0].Id);
    }

    [Fact]
    public void MigrateNpc_MarksBothChunksDirty()
    {
        var map = new WorldMap(42);
        var chunkA = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var chunkB = new Chunk(ChunkPosition.FromCoords(1, 0, Position.DefaultZ));
        map.AddChunk(chunkA);
        map.AddChunk(chunkB);

        var npc = new TownNpcEntity(map.AllocateEntityId())
        {
            Position = Position.FromCoords(5, 5, Position.DefaultZ),
            Health = new Health(100),
            CombatStats = default,
            NpcData = new TownNpcTag { Name = "Test" },
        };
        chunkA.AddEntity(npc);

        chunkA.ClearSaveFlag();
        chunkB.ClearSaveFlag();

        map.MoveNpcEntity(npc.Id, npc.Position, Position.FromCoords(Chunk.Size + 3, 5, Position.DefaultZ));

        Assert.True(chunkA.IsModifiedSinceLastSave);
        Assert.True(chunkB.IsModifiedSinceLastSave);
        Assert.Empty(chunkA.TownNpcs.ToArray());
        Assert.Single(chunkB.TownNpcs.ToArray());
        Assert.Equal(npc.Id, chunkB.TownNpcs[0].Id);
    }

    [Fact]
    public void MoveNpcEntity_SameChunk_UpdatesPosition()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);

        var npc = new TownNpcEntity(map.AllocateEntityId())
        {
            Position = Position.FromCoords(5, 5, Position.DefaultZ),
            Health = new Health(100),
            CombatStats = default,
            NpcData = new TownNpcTag { Name = "Test" },
        };
        chunk.AddEntity(npc);

        var newPos = Position.FromCoords(6, 5, Position.DefaultZ);
        map.MoveNpcEntity(npc.Id, npc.Position, newPos);

        Assert.Single(chunk.TownNpcs.ToArray());
        Assert.Equal(newPos, chunk.TownNpcs[0].Position);
    }

    [Fact]
    public void MigrateMonster_SameChunk_Dirty()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);

        var monster = new MonsterEntity(map.AllocateEntityId())
        {
            Position = Position.FromCoords(5, 5, Position.DefaultZ),
            Health = new Health(10),
            CombatStats = default,
            MonsterData = new MonsterData { MonsterTypeId = 1 },
        };
        chunk.AddEntity(monster);
        chunk.ClearSaveFlag();

        // Move within same chunk
        map.MoveMonsterEntity(monster.Id, monster.Position, Position.FromCoords(6, 5, Position.DefaultZ));

        Assert.True(chunk.IsModifiedSinceLastSave);
    }

    // ── Player management ──

    [Fact]
    public void AddPlayer_AndGetPlayer()
    {
        var map = new WorldMap(42);
        var player = new PlayerEntity(map.AllocateEntityId())
        {
            Position = Position.FromCoords(5, 5, Position.DefaultZ),
            Health = new Health(100),
            ConnectionId = 1,
        };
        map.AddPlayer(player);
        var got = map.GetPlayer(player.Id);
        Assert.NotNull(got);
        Assert.Equal(player.Id, got.Value.Id);
    }

    [Fact]
    public void AddPlayer_UpdateExisting()
    {
        var map = new WorldMap(42);
        var player = new PlayerEntity(map.AllocateEntityId())
        {
            Position = Position.FromCoords(5, 5, Position.DefaultZ),
            Health = new Health(100),
            ConnectionId = 1,
        };
        map.AddPlayer(player);
        // Re-add with updated position
        player.Position = Position.FromCoords(10, 10, Position.DefaultZ);
        map.AddPlayer(player);
        Assert.Equal(1, map.Players.Length);
    }

    [Fact]
    public void RemovePlayer_RemovesCorrectly()
    {
        var map = new WorldMap(42);
        var player = new PlayerEntity(map.AllocateEntityId())
        {
            Position = Position.FromCoords(5, 5, Position.DefaultZ),
            Health = new Health(100),
            ConnectionId = 1,
        };
        map.AddPlayer(player);
        map.RemovePlayer(player.Id);
        Assert.Null(map.GetPlayer(player.Id));
        Assert.Equal(0, map.Players.Length);
    }

    [Fact]
    public void RemovePlayer_NonExistent_NoError()
    {
        var map = new WorldMap(42);
        map.RemovePlayer(999); // Should not throw
    }

    [Fact]
    public void GetPlayerRef_Throws_WhenNotFound()
    {
        var map = new WorldMap(42);
        Assert.Throws<KeyNotFoundException>(() => map.GetPlayerRef(999));
    }

    [Fact]
    public void GetPlayerByConnection_ReturnsCorrectPlayer()
    {
        var map = new WorldMap(42);
        var player = new PlayerEntity(map.AllocateEntityId())
        {
            Position = Position.FromCoords(5, 5, Position.DefaultZ),
            Health = new Health(100),
            ConnectionId = 42,
        };
        map.AddPlayer(player);
        var found = map.GetPlayerByConnection(42);
        Assert.NotNull(found);
        Assert.Equal(player.Id, found.Value.Id);
    }

    [Fact]
    public void GetPlayerByConnection_ReturnsNull_WhenNotFound()
    {
        var map = new WorldMap(42);
        Assert.Null(map.GetPlayerByConnection(999));
    }

    // ── Entity queries ──

    [Fact]
    public void GetEntityRefAt_FindsPlayer()
    {
        var map = new WorldMap(42);
        var pos = Position.FromCoords(5, 5, Position.DefaultZ);
        var player = new PlayerEntity(map.AllocateEntityId())
        {
            Position = pos,
            Health = new Health(100),
            ConnectionId = 1,
        };
        map.AddPlayer(player);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);

        var eRef = map.GetEntityRefAt(pos);
        Assert.Equal(EntityType.Player, eRef.Type);
        Assert.Equal(player.Id, eRef.Id);
    }

    [Fact]
    public void GetEntityRefAt_FindsMonster()
    {
        var map = new WorldMap(42);
        var pos = Position.FromCoords(5, 5, Position.DefaultZ);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        var monster = new MonsterEntity(map.AllocateEntityId())
        {
            Position = pos,
            Health = new Health(10),
            CombatStats = default,
            MonsterData = new MonsterData { MonsterTypeId = 1 },
        };
        chunk.AddEntity(monster);

        var eRef = map.GetEntityRefAt(pos);
        Assert.Equal(EntityType.Monster, eRef.Type);
        Assert.Equal(monster.Id, eRef.Id);
    }

    [Fact]
    public void GetEntityRefAt_FindsGroundItem()
    {
        var map = new WorldMap(42);
        var pos = Position.FromCoords(5, 5, Position.DefaultZ);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        var item = new GroundItemEntity(map.AllocateEntityId())
        {
            Position = pos,
            Item = new ItemData { ItemTypeId = 1, StackCount = 1 },
        };
        chunk.AddEntity(item);

        var eRef = map.GetEntityRefAt(pos);
        Assert.Equal(EntityType.GroundItem, eRef.Type);
    }

    [Fact]
    public void GetEntityRefAt_FindsCrop()
    {
        var map = new WorldMap(42);
        var pos = Position.FromCoords(5, 5, Position.DefaultZ);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        var crop = new CropEntity(map.AllocateEntityId())
        {
            Position = pos,
        };
        chunk.AddEntity(crop);

        var eRef = map.GetEntityRefAt(pos);
        Assert.Equal(EntityType.Crop, eRef.Type);
    }

    [Fact]
    public void GetEntityRefAt_FindsAnimal()
    {
        var map = new WorldMap(42);
        var pos = Position.FromCoords(5, 5, Position.DefaultZ);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        var animal = new AnimalEntity(map.AllocateEntityId())
        {
            Position = pos,
            Health = new Health(10),
        };
        chunk.AddEntity(animal);

        var eRef = map.GetEntityRefAt(pos);
        Assert.Equal(EntityType.Animal, eRef.Type);
    }

    [Fact]
    public void GetEntityRefAt_FindsTownNpc()
    {
        var map = new WorldMap(42);
        var pos = Position.FromCoords(5, 5, Position.DefaultZ);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        var npc = new TownNpcEntity(map.AllocateEntityId())
        {
            Position = pos,
            Health = new Health(100),
        };
        chunk.AddEntity(npc);

        var eRef = map.GetEntityRefAt(pos);
        Assert.Equal(EntityType.TownNpc, eRef.Type);
    }

    [Fact]
    public void GetEntityRefAt_FindsResourceNode()
    {
        var map = new WorldMap(42);
        var pos = Position.FromCoords(5, 5, Position.DefaultZ);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        var node = new ResourceNodeEntity(map.AllocateEntityId())
        {
            Position = pos,
            Health = new Health(50),
        };
        chunk.AddEntity(node);

        var eRef = map.GetEntityRefAt(pos);
        Assert.Equal(EntityType.ResourceNode, eRef.Type);
    }

    [Fact]
    public void GetEntityRefAt_ReturnsDefault_WhenEmpty()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        var eRef = map.GetEntityRefAt(Position.FromCoords(5, 5, Position.DefaultZ));
        Assert.Equal(EntityType.None, eRef.Type);
    }

    [Fact]
    public void GetAllEntityRefsAt_ReturnsMultiple()
    {
        var map = new WorldMap(42);
        var pos = Position.FromCoords(5, 5, Position.DefaultZ);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);

        // A ground item and a crop at same position
        chunk.AddEntity(new GroundItemEntity(map.AllocateEntityId()) { Position = pos, Item = new ItemData { ItemTypeId = 1 } });
        chunk.AddEntity(new CropEntity(map.AllocateEntityId()) { Position = pos });

        var refs = map.GetAllEntityRefsAt(pos);
        Assert.Equal(2, refs.Length);
    }

    [Fact]
    public void GetAllEntityRefsAt_IncludesAllEntityTypes()
    {
        var map = new WorldMap(42);
        var pos = Position.FromCoords(5, 5, Position.DefaultZ);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);

        // Add one of each entity type at the same position
        chunk.AddEntity(new MonsterEntity(map.AllocateEntityId()) { Position = pos, Health = new Health(10) });
        chunk.AddEntity(new TownNpcEntity(map.AllocateEntityId()) { Position = pos, Health = new Health(10) });
        chunk.AddEntity(new ResourceNodeEntity(map.AllocateEntityId()) { Position = pos, Health = new Health(10) });
        chunk.AddEntity(new GroundItemEntity(map.AllocateEntityId()) { Position = pos, Item = new ItemData { ItemTypeId = 1 } });
        chunk.AddEntity(new CropEntity(map.AllocateEntityId()) { Position = pos });
        chunk.AddEntity(new AnimalEntity(map.AllocateEntityId()) { Position = pos, Health = new Health(10) });

        var refs = map.GetAllEntityRefsAt(pos);
        Assert.Equal(6, refs.Length);
        Assert.Contains(refs, r => r.Type == EntityType.Monster);
        Assert.Contains(refs, r => r.Type == EntityType.TownNpc);
        Assert.Contains(refs, r => r.Type == EntityType.ResourceNode);
        Assert.Contains(refs, r => r.Type == EntityType.GroundItem);
        Assert.Contains(refs, r => r.Type == EntityType.Crop);
        Assert.Contains(refs, r => r.Type == EntityType.Animal);
    }

    [Fact]
    public void GetAllEntityRefsAt_ReturnsEmpty_WhenNone()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        var refs = map.GetAllEntityRefsAt(Position.FromCoords(5, 5, Position.DefaultZ));
        Assert.Empty(refs);
    }

    [Fact]
    public void GetAllEntityRefsAt_NoChunk_ReturnsEmpty()
    {
        var map = new WorldMap(42);
        var refs = map.GetAllEntityRefsAt(Position.FromCoords(5, 5, Position.DefaultZ));
        Assert.Empty(refs);
    }

    [Fact]
    public void GetGroundItemRef_Throws_WhenNotFound()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        Assert.Throws<KeyNotFoundException>(() => map.GetGroundItemRef(999));
    }

    [Fact]
    public void GetMonsterRef_Throws_WhenNotFound()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        Assert.Throws<KeyNotFoundException>(() => map.GetMonsterRef(999));
    }

    [Fact]
    public void GetResourceNodeRef_Throws_WhenNotFound()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        Assert.Throws<KeyNotFoundException>(() => map.GetResourceNodeRef(999));
    }

    [Fact]
    public void GetResourceNodeRef_ReturnsRef_WhenFound()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        var pos = Position.FromCoords(5, 5, Position.DefaultZ);
        var node = new ResourceNodeEntity(map.AllocateEntityId()) { Position = pos };
        ref var added = ref chunk.AddEntity(node);
        ref var found = ref map.GetResourceNodeRef(added.Id);
        Assert.Equal(pos, found.Position);
    }

    [Fact]
    public void GetTownNpcRef_Throws_WhenNotFound()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        Assert.Throws<KeyNotFoundException>(() => map.GetTownNpcRef(999));
    }

    [Fact]
    public void GetTownNpcRef_ReturnsRef_WhenFound()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        var pos = Position.FromCoords(5, 5, Position.DefaultZ);
        var npc = new TownNpcEntity(map.AllocateEntityId()) { Position = pos };
        ref var added = ref chunk.AddEntity(npc);
        ref var found = ref map.GetTownNpcRef(added.Id);
        Assert.Equal(pos, found.Position);
    }

    // ── Spatial queries ──

    [Fact]
    public void CollectEntitiesPositions_IncludesPlayersAndMonsters()
    {
        var map = new WorldMap(42);
        var pos1 = Position.FromCoords(5, 5, Position.DefaultZ);
        var pos2 = Position.FromCoords(10, 10, Position.DefaultZ);
        map.AddPlayer(new PlayerEntity(map.AllocateEntityId()) { Position = pos1, Health = new Health(100), ConnectionId = 1 });
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.AddEntity(new MonsterEntity(map.AllocateEntityId()) { Position = pos2, Health = new Health(10), MonsterData = new MonsterData { MonsterTypeId = 1 } });
        map.AddChunk(chunk);

        var positions = new HashSet<long>();
        map.CollectEntitiesPositions(positions);
        Assert.Contains(Position.PackCoord(5, 5, Position.DefaultZ), positions);
        Assert.Contains(Position.PackCoord(10, 10, Position.DefaultZ), positions);
    }

    [Fact]
    public void IsPositionOccupiedByEntity_ReturnsTrue_ForPlayer()
    {
        var map = new WorldMap(42);
        var pos = Position.FromCoords(5, 5, Position.DefaultZ);
        map.AddPlayer(new PlayerEntity(map.AllocateEntityId()) { Position = pos, Health = new Health(100), ConnectionId = 1 });
        Assert.True(map.IsPositionOccupiedByEntity(pos));
    }

    [Fact]
    public void IsPositionOccupiedByEntity_ReturnsFalse_WhenEmpty()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        Assert.False(map.IsPositionOccupiedByEntity(Position.FromCoords(5, 5, Position.DefaultZ)));
    }

    [Fact]
    public void IsPositionOccupiedByEntity_ReturnsFalse_NoChunk()
    {
        var map = new WorldMap(42);
        Assert.False(map.IsPositionOccupiedByEntity(Position.FromCoords(5, 5, Position.DefaultZ)));
    }

    // ── Animal migration ──

    [Fact]
    public void MoveAnimalEntity_CrossChunk()
    {
        var map = new WorldMap(42);
        var chunkA = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var chunkB = new Chunk(ChunkPosition.FromCoords(1, 0, Position.DefaultZ));
        map.AddChunk(chunkA);
        map.AddChunk(chunkB);

        var animal = new AnimalEntity(map.AllocateEntityId())
        {
            Position = Position.FromCoords(5, 5, Position.DefaultZ),
            Health = new Health(10),
        };
        chunkA.AddEntity(animal);
        chunkA.ClearSaveFlag();
        chunkB.ClearSaveFlag();

        map.MoveAnimalEntity(animal.Id, animal.Position, Position.FromCoords(Chunk.Size + 3, 5, Position.DefaultZ));

        Assert.True(chunkA.IsModifiedSinceLastSave);
        Assert.True(chunkB.IsModifiedSinceLastSave);
        Assert.Empty(chunkA.Animals.ToArray());
        Assert.Single(chunkB.Animals.ToArray());
    }

    // ── Other WorldMap methods ──

    [Fact]
    public void SetNextEntityId_SetsCorrectly()
    {
        var map = new WorldMap(42);
        map.SetNextEntityId(100);
        Assert.Equal(100, map.AllocateEntityId());
        Assert.Equal(101, map.AllocateEntityId());
    }

    [Fact]
    public void ExistsChunk_ReturnsTrueForLoaded()
    {
        var map = new WorldMap(42);
        var gen = new BspDungeonGenerator(42);
        map.GetOrCreateChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ), gen);
        Assert.True(map.ExistsChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ), gen));
    }

    [Fact]
    public void ExistsChunk_CachesNegativeResult()
    {
        var map = new WorldMap(42);
        var gen = new BspDungeonGenerator(42);
        // First call should check generator; the BSP generator always generates, 
        // but for an unloaded chunk the generator Exists() may return true or false
        var result1 = map.ExistsChunk(ChunkPosition.FromCoords(100, 100, Position.DefaultZ), gen);
        // Calling again should use cached result
        var result2 = map.ExistsChunk(ChunkPosition.FromCoords(100, 100, Position.DefaultZ), gen);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void GetChunk_Throws_WhenMissing()
    {
        var map = new WorldMap(42);
        Assert.Throws<KeyNotFoundException>(() => map.GetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ)));
    }

    [Fact]
    public void GetChunk_ReturnsLoaded()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        var got = map.GetChunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        Assert.Same(chunk, got);
    }

    [Fact]
    public void IsTransparent_ReturnsTrueForMissingChunk()
    {
        var map = new WorldMap(42);
        // Default tile (Void type) is transparent since it's not Blocked
        Assert.True(map.IsTransparent(Position.FromCoords(0, 0, Position.DefaultZ)));
    }

    [Fact]
    public void SetPlaceable_UpdatesTile()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.Tiles[5, 5] = MakeFloor();
        map.AddChunk(chunk);

        map.SetPlaceable(Position.FromCoords(5, 5, Position.DefaultZ), 42, 7);
        var tile = map.GetTile(Position.FromCoords(5, 5, Position.DefaultZ));
        Assert.Equal(42, tile.PlaceableItemId);
        Assert.Equal(7, tile.PlaceableItemExtra);
    }

    [Fact]
    public void SetTileChunkDirty_MarksModified()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        map.AddChunk(chunk);
        chunk.ClearSaveFlag();

        map.SetTileChunkDirty(Position.FromCoords(5, 5, Position.DefaultZ));
        Assert.True(chunk.IsModifiedSinceLastSave);
    }

    [Fact]
    public void GetModifiedChunks_ReturnsOnlyModified()
    {
        var map = new WorldMap(42);
        var chunk1 = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var chunk2 = new Chunk(ChunkPosition.FromCoords(1, 0, Position.DefaultZ));
        map.AddChunk(chunk1);
        map.AddChunk(chunk2);

        chunk1.MarkModified();
        chunk2.ClearSaveFlag();

        var modified = map.GetModifiedChunks();
        Assert.Single(modified);
        Assert.Same(chunk1, modified[0]);
    }

    [Fact]
    public void FlushDirtyTiles_CollectsDirtyTiles()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.Tiles[5, 5] = MakeFloor();
        map.AddChunk(chunk);

        map.SetTile(Position.FromCoords(5, 5, Position.DefaultZ), MakeClosedDoor());

        var result = new List<(Position Pos, TileInfo Tile)>();
        map.FlushDirtyTiles(result);
        Assert.Single(result);

        // Second flush should return empty
        result.Clear();
        map.FlushDirtyTiles(result);
        Assert.Empty(result);
    }

    [Fact]
    public void OpenDoor_OnNonDoor_NoOp()
    {
        var map = new WorldMap(42);
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.Tiles[5, 5] = MakeFloor();
        map.AddChunk(chunk);

        map.OpenDoor(Position.FromCoords(5, 5, Position.DefaultZ));
        var tile = map.GetTile(Position.FromCoords(5, 5, Position.DefaultZ));
        Assert.Equal(0, tile.PlaceableItemExtra);
    }
}
