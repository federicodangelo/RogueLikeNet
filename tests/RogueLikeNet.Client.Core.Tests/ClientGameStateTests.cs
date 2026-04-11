using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Tests;

public class ClientGameStateTests
{
    /// <summary>
    /// Creates a chunk with all Floor tiles so FOV has no obstructions.
    /// </summary>
    private static ChunkDataMsg MakeFloorChunk(int cx, int cy, int cz = 0)
    {
        int size = Chunk.Size * Chunk.Size;
        var tileIds = new int[size];
        var light = new int[size];
        int floorId = GameData.Instance.Tiles.GetNumericId("floor");
        for (int i = 0; i < size; i++)
        {
            tileIds[i] = floorId;
            light[i] = 5;
        }
        return new ChunkDataMsg
        {
            ChunkX = cx,
            ChunkY = cy,
            ChunkZ = cz,
            TileIds = tileIds,
            TilePlaceableItemExtras = new int[size],
            TilePlaceableItemIds = new int[size],
        };
    }

    private static WorldDeltaMsg MakeSnapshot(int playerX = 32, int playerY = 32)
    {
        return new WorldDeltaMsg
        {
            FromTick = 0,
            ToTick = 1,
            IsSnapshot = true,
            Chunks = [MakeFloorChunk(0, 0)],
            EntityUpdates = [new EntityUpdateMsg { Id = 1, X = playerX, Y = playerY, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
            PlayerState = new PlayerStateMsg { PlayerEntityId = 1 },
        };
    }
    [Fact]
    public void ApplyDelta_QueuesCombatEvents()
    {
        var state = new ClientGameState();

        // Apply a snapshot first to initialize
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 0,
            ToTick = 1,
            IsSnapshot = true,
            Chunks = [],
            EntityUpdates = [new EntityUpdateMsg { Id = 1, X = 5, Y = 5, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
            PlayerState = new PlayerStateMsg { PlayerEntityId = 1 },
        });

        var delta = new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            TileUpdates = [],
            EntityUpdates = [],
            CombatEvents =
            [
                new CombatEventMsg { AttackerX = 5, AttackerY = 5, TargetX = 6, TargetY = 5, Damage = 15, TargetDied = false },
                new CombatEventMsg { AttackerX = 5, AttackerY = 5, TargetX = 7, TargetY = 5, Damage = 30, TargetDied = true },
            ],
        };

        state.ApplyDelta(delta);

        Assert.Equal(2, state.PendingCombatEvents.Count);
        Assert.Equal(15, state.PendingCombatEvents[0].Damage);
        Assert.True(state.PendingCombatEvents[1].TargetDied);
    }

    [Fact]
    public void DrainCombatEvents_ClearsPending()
    {
        var state = new ClientGameState();
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 0,
            ToTick = 1,
            IsSnapshot = true,
            Chunks = [],
            EntityUpdates = [new EntityUpdateMsg { Id = 1, X = 5, Y = 5, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
            PlayerState = new PlayerStateMsg { PlayerEntityId = 1 },
        });

        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            TileUpdates = [],
            EntityUpdates = [],
            CombatEvents = [new CombatEventMsg { AttackerX = 5, AttackerY = 5, TargetX = 6, TargetY = 5, Damage = 10 }],
        });

        Assert.Single(state.PendingCombatEvents);
        state.DrainCombatEvents();
        Assert.Empty(state.PendingCombatEvents);
    }

    [Fact]
    public void Clear_ResetsCombatEvents()
    {
        var state = new ClientGameState();
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 0,
            ToTick = 1,
            IsSnapshot = true,
            Chunks = [],
            EntityUpdates = [new EntityUpdateMsg { Id = 1, X = 5, Y = 5, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
            PlayerState = new PlayerStateMsg { PlayerEntityId = 1 },
        });

        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            TileUpdates = [],
            EntityUpdates = [],
            CombatEvents = [new CombatEventMsg { AttackerX = 5, AttackerY = 5, TargetX = 6, TargetY = 5, Damage = 10 }],
        });

        state.Clear();
        Assert.Empty(state.PendingCombatEvents);
    }

    [Fact]
    public void Chunks_ExposedViaReadOnlyDictionary()
    {
        var state = new ClientGameState();
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 0,
            ToTick = 1,
            IsSnapshot = true,
            Chunks =
            [
                new ChunkDataMsg
                {
                    ChunkX = 0,
                    ChunkY = 0,
                    TileIds = new int[64 * 64],
                    TilePlaceableItemExtras = new int[64 * 64],
                    TilePlaceableItemIds = new int[64 * 64],
                }
            ],
            EntityUpdates = [],
        });

        Assert.Single(state.Chunks);
    }

    // === Visibility / FOV Tests ===

    [Fact]
    public void ApplySnapshot_ComputesVisibility()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));

        // Player's own tile should be visible
        Assert.True(state.IsVisible(32, 32));
        // Adjacent floor tiles should also be visible (no walls)
        Assert.True(state.IsVisible(33, 32));
        Assert.True(state.IsVisible(32, 33));
    }

    [Fact]
    public void IsExplored_ClearedAfterClear_OfVisibleTiles()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));

        Assert.True(state.IsExplored(32, 32));
        Assert.True(state.IsVisible(32, 32));

        // After a new snapshot at a far location, old tiles become explored but not visible
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 0,
            ToTick = 2,
            IsSnapshot = true,
            Chunks = [MakeFloorChunk(0, 0)],
            EntityUpdates = [new EntityUpdateMsg { Id = 1, X = 10, Y = 10, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
            PlayerState = new PlayerStateMsg { PlayerEntityId = 1 },
        });

        // Old position is explored but not visible
        Assert.False(state.IsExplored(32, 32));
        Assert.False(state.IsVisible(32, 32));
        // New position is both
        Assert.True(state.IsVisible(10, 10));
        Assert.True(state.IsExplored(10, 10));
    }

    [Fact]
    public void Clear_ResetsVisibilityAndExplored()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));

        Assert.True(state.IsVisible(32, 32));
        Assert.True(state.IsExplored(32, 32));

        state.Clear();

        Assert.False(state.IsVisible(32, 32));
        Assert.False(state.IsExplored(32, 32));
        Assert.Equal(0, state.PlayerX);
        Assert.Equal(0, state.PlayerY);
    }

    // === ApplyDelta Tests ===

    [Fact]
    public void ApplyDelta_AddEntity()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));

        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [new EntityUpdateMsg { Id = 99, X = 35, Y = 35, GlyphId = 103, FgColor = 0xFF0000, Health = 20, MaxHealth = 20 }],
        });

        Assert.True(state.Entities.ContainsKey(99));
        Assert.Equal(35, state.Entities[99].X);
    }

    [Fact]
    public void ApplyDelta_UpdateEntity()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));

        // Add entity
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [new EntityUpdateMsg { Id = 99, X = 35, Y = 35, GlyphId = 103, FgColor = 0xFF0000, Health = 20, MaxHealth = 20 }],
        });

        // Update entity position
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 2,
            ToTick = 3,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [new EntityUpdateMsg { Id = 99, X = 36, Y = 36, GlyphId = 103, FgColor = 0xFF0000, Health = 15, MaxHealth = 20 }],
        });

        Assert.Equal(36, state.Entities[99].X);
        Assert.Equal(15, state.Entities[99].Health);
    }

    [Fact]
    public void ApplyDelta_RemoveEntity()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));

        // Add and then remove
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 2,
            ToTick = 3,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [],
            EntityRemovals = [new EntityRemovedMsg { Id = 99 }],
        });

        Assert.False(state.Entities.ContainsKey(99));
    }

    [Fact]
    public void ApplyDelta_TileUpdates()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));

        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            CombatEvents = [],
            EntityUpdates = [],
            TileUpdates = [new TileUpdateMsg { X = 32, Y = 32, TileId = GameData.Instance.Tiles.GetNumericId("wall") }],
        });

        var (tile, lightlevel) = state.GetTileAndLightLevel(32, 32);
        Assert.Equal(TileType.Blocked, tile.Type);
        Assert.Equal(GameData.Instance.Tiles.GetGlyphId(tile.TileId), tile.GlyphId);
        // LightLevel is now computed client-side (player at 32,32 illuminates this tile)
        Assert.True(lightlevel > 0);
    }

    [Fact]
    public void ApplyDelta_PlayerPositionTrackedFromEntity()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));

        Assert.Equal(32, state.PlayerX);
        Assert.Equal(32, state.PlayerY);

        // Move the player entity (glyph 64 = '@')
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [new EntityUpdateMsg { Id = 1, X = 40, Y = 40, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
        });

        Assert.Equal(40, state.PlayerX);
        Assert.Equal(40, state.PlayerY);
    }

    [Fact]
    public void ApplyDelta_UpdatesChunks()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));

        // Send a delta with updated chunk data
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [MakeFloorChunk(0, 0)],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [],
        });

        Assert.Equal(2, state.WorldTick);
    }

    [Fact]
    public void ApplyDelta_PlayerState_Updated()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));

        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [],
            PlayerState = new PlayerStateMsg { Health = 80, MaxHealth = 100, Attack = 12, Defense = 6, Level = 2 },
        });

        Assert.NotNull(state.PlayerState);
        Assert.Equal(80, state.PlayerState.Health);
    }

    [Fact]
    public void ApplyDelta_NullPlayerState_KeepsExisting()
    {
        var state = new ClientGameState();
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 0,
            ToTick = 1,
            IsSnapshot = true,
            Chunks = [MakeFloorChunk(0, 0)],
            EntityUpdates = [new EntityUpdateMsg { Id = 1, X = 32, Y = 32, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
            PlayerState = new PlayerStateMsg { Health = 90, MaxHealth = 100, PlayerEntityId = 1 },
        });

        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [],
            PlayerState = null,
        });

        Assert.NotNull(state.PlayerState);
        Assert.Equal(90, state.PlayerState.Health);
    }

    [Fact]
    public void ApplyDelta_RecomputesVisibility()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));

        Assert.True(state.IsVisible(32, 32));

        // Move player far away via delta
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [new EntityUpdateMsg { Id = 1, X = 10, Y = 10, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
            PlayerState = new PlayerStateMsg { PlayerEntityId = 1 },
        });

        // Old position no longer visible, new is
        Assert.False(state.IsVisible(32, 32));
        Assert.True(state.IsVisible(10, 10));
        // Old position still explored
        Assert.True(state.IsExplored(32, 32));
    }

    [Fact]
    public void GetTile_MissingChunk_ReturnsDefault()
    {
        var state = new ClientGameState();
        var tile = state.GetTile(9999, 9999);
        Assert.Equal(TileType.Void, tile.Type);
    }

    [Fact]
    public void GetFloorItems_NoItems_ReturnsEmpty()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));
        var items = state.GetFloorItems();
        Assert.Empty(items);
    }

    [Fact]
    public void GetFloorItems_ItemAtPlayerPosition_ReturnsItem()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));

        // Add an item entity at the player's position
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [new EntityUpdateMsg
            {
                Id = 99,
                X = 32,
                Y = 32,
                GlyphId = 33,
                FgColor = 0x00FF00,
                Health = 0,
                MaxHealth = 0,
                Item = new ItemDataMsg { ItemTypeId = 5 }
            }],
        });

        var items = state.GetFloorItems();
        Assert.Single(items);
        Assert.Equal(5, items[0]);
    }

    [Fact]
    public void GetFloorItems_ItemAtDifferentPosition_ReturnsEmpty()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));

        // Add an item entity at a different position
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [new EntityUpdateMsg
            {
                Id = 99,
                X = 50,
                Y = 50,
                GlyphId = 33,
                FgColor = 0x00FF00,
                Health = 0,
                MaxHealth = 0,
                Item = new ItemDataMsg { ItemTypeId = 5 }
            }],
        });

        var items = state.GetFloorItems();
        Assert.Empty(items);
    }

    [Fact]
    public void ApplyDelta_PositionHealthUpdate_UpdatesExistingEntity()
    {
        var state = new ClientGameState();
        state.ApplyDelta(MakeSnapshot(32, 32));

        // Add entity
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [new EntityUpdateMsg { Id = 99, X = 35, Y = 35, GlyphId = 103, FgColor = 0xFF0000, Health = 20, MaxHealth = 20 }],
        });

        // Send a position-health-only update
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 2,
            ToTick = 3,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [],
            EntityPositionHealthUpdates = [new EntityPositionHealthMsg { Id = 99, X = 36, Y = 37, Health = 15 }],
        });

        Assert.Equal(36, state.Entities[99].X);
        Assert.Equal(37, state.Entities[99].Y);
        Assert.Equal(15, state.Entities[99].Health);
        // GlyphId and FgColor should remain from original
        Assert.Equal(103, state.Entities[99].GlyphId);
        Assert.Equal(0xFF0000, state.Entities[99].FgColor);
    }

    [Fact]
    public void ApplyDelta_LightSourceEntity_ComputesLighting()
    {
        var state = new ClientGameState();
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 0,
            ToTick = 1,
            IsSnapshot = true,
            Chunks = [MakeFloorChunk(0, 0)],
            EntityUpdates =
            [
                new EntityUpdateMsg { Id = 1, X = 32, Y = 32, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 },
                new EntityUpdateMsg { Id = 2, X = 35, Y = 32, GlyphId = 42, FgColor = 0xFFAA00, LightRadius = 5 },
            ],
            PlayerState = new PlayerStateMsg { PlayerEntityId = 1 },
        });

        // The light source at (35,32) should illuminate nearby tiles
        var lightLevel = state.GetLightLevel(35, 32);
        Assert.True(lightLevel > 0, "Tile at light source should be illuminated");
    }

    [Fact]
    public void ApplyChunkData_StoresChunkCorrectly()
    {
        var state = new ClientGameState();

        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 0,
            ToTick = 1,
            IsSnapshot = true,
            Chunks = [MakeFloorChunk(0, 0)],
            EntityUpdates = [],
        });

        Assert.Single(state.Chunks);
        var tile = state.GetTile(5, 5);
        Assert.Equal(TileType.Floor, tile.Type);
    }

    [Fact]
    public void WorldTick_TracksLatestTick()
    {
        var state = new ClientGameState();
        Assert.Equal(0, state.WorldTick);
        state.ApplyDelta(MakeSnapshot(32, 32));
        Assert.Equal(1, state.WorldTick);

        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 5,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [],
        });
        Assert.Equal(5, state.WorldTick);
    }

    [Fact]
    public void ApplyDelta_DiscardedChunkKeys_RemovesChunks()
    {
        var state = new ClientGameState();
        // Apply snapshot with two chunks
        var key0 = RogueLikeNet.Core.Components.Position.PackCoord(0, 0, 0);
        var key1 = RogueLikeNet.Core.Components.Position.PackCoord(1, 0, 0);

        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 0,
            ToTick = 1,
            IsSnapshot = true,
            Chunks = [MakeFloorChunk(0, 0), MakeFloorChunk(1, 0)],
            EntityUpdates = [new EntityUpdateMsg { Id = 1, X = 32, Y = 32, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
            PlayerState = new PlayerStateMsg { PlayerEntityId = 1 },
        });

        Assert.Equal(2, state.Chunks.Count);

        // Apply delta that discards chunk (1,0)
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [],
            DiscardedChunkKeys = [key1],
        });

        Assert.Single(state.Chunks);
        Assert.True(state.Chunks.ContainsKey(key0));
        Assert.False(state.Chunks.ContainsKey(key1));
    }
}
