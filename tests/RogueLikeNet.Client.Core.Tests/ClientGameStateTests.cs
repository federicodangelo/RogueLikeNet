using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Tests;

public class ClientGameStateTests
{
    /// <summary>
    /// Creates a chunk with all Floor tiles so FOV has no obstructions.
    /// </summary>
    private static ChunkDataMsg MakeFloorChunk(int cx, int cy)
    {
        int size = Chunk.Size * Chunk.Size;
        var types = new byte[size];
        var glyphs = new int[size];
        var fg = new int[size];
        var bg = new int[size];
        var light = new int[size];
        for (int i = 0; i < size; i++)
        {
            types[i] = (byte)TileType.Floor;
            glyphs[i] = '.';
            light[i] = 5;
        }
        return new ChunkDataMsg
        {
            ChunkX = cx,
            ChunkY = cy,
            TileTypes = types,
            TileGlyphs = glyphs,
            TileFgColors = fg,
            TileBgColors = bg,
        };
    }

    private static WorldSnapshotMsg MakeSnapshot(int playerX = 32, int playerY = 32)
    {
        return new WorldSnapshotMsg
        {
            WorldTick = 1,
            PlayerEntityId = 1,
            PlayerX = playerX,
            PlayerY = playerY,
            Chunks = [MakeFloorChunk(0, 0)],
            Entities = [new EntityMsg { Id = 1, X = playerX, Y = playerY, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
        };
    }
    [Fact]
    public void ApplyDelta_QueuesCombatEvents()
    {
        var state = new ClientGameState();

        // Apply a snapshot first to initialize
        state.ApplySnapshot(new WorldSnapshotMsg
        {
            WorldTick = 1,
            Chunks = [],
            Entities = [new EntityMsg { Id = 1, X = 5, Y = 5, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
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
        state.ApplySnapshot(new WorldSnapshotMsg
        {
            WorldTick = 1,
            Chunks = [],
            Entities = [new EntityMsg { Id = 1, X = 5, Y = 5, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
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
        state.ApplySnapshot(new WorldSnapshotMsg
        {
            WorldTick = 1,
            Chunks = [],
            Entities = [new EntityMsg { Id = 1, X = 5, Y = 5, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
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
        state.ApplySnapshot(new WorldSnapshotMsg
        {
            WorldTick = 1,
            Chunks =
            [
                new ChunkDataMsg
                {
                    ChunkX = 0,
                    ChunkY = 0,
                    TileTypes = new byte[64 * 64],
                    TileGlyphs = new int[64 * 64],
                    TileFgColors = new int[64 * 64],
                    TileBgColors = new int[64 * 64],
                }
            ],
            Entities = [],
        });

        Assert.Single(state.Chunks);
    }

    // === Visibility / FOV Tests ===

    [Fact]
    public void ApplySnapshot_ComputesVisibility()
    {
        var state = new ClientGameState();
        state.ApplySnapshot(MakeSnapshot(32, 32));

        // Player's own tile should be visible
        Assert.True(state.IsVisible(32, 32));
        // Adjacent floor tiles should also be visible (no walls)
        Assert.True(state.IsVisible(33, 32));
        Assert.True(state.IsVisible(32, 33));
    }

    [Fact]
    public void IsExplored_PersistsAfterClear_OfVisibleTiles()
    {
        var state = new ClientGameState();
        state.ApplySnapshot(MakeSnapshot(32, 32));

        Assert.True(state.IsExplored(32, 32));
        Assert.True(state.IsVisible(32, 32));

        // After a new snapshot at a far location, old tiles become explored but not visible
        state.ApplySnapshot(new WorldSnapshotMsg
        {
            WorldTick = 2,
            PlayerX = 10,
            PlayerY = 10,
            Chunks = [MakeFloorChunk(0, 0)],
            Entities = [new EntityMsg { Id = 1, X = 10, Y = 10, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
        });

        // Old position is explored but not visible
        Assert.True(state.IsExplored(32, 32));
        Assert.False(state.IsVisible(32, 32));
        // New position is both
        Assert.True(state.IsVisible(10, 10));
        Assert.True(state.IsExplored(10, 10));
    }

    [Fact]
    public void Clear_ResetsVisibilityAndExplored()
    {
        var state = new ClientGameState();
        state.ApplySnapshot(MakeSnapshot(32, 32));

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
        state.ApplySnapshot(MakeSnapshot(32, 32));

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
        state.ApplySnapshot(MakeSnapshot(32, 32));

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
        state.ApplySnapshot(MakeSnapshot(32, 32));

        // Add and then remove
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [new EntityUpdateMsg { Id = 99, X = 35, Y = 35, GlyphId = 103 }],
        });
        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 2,
            ToTick = 3,
            Chunks = [],
            TileUpdates = [],
            CombatEvents = [],
            EntityUpdates = [new EntityUpdateMsg { Id = 99, Removed = true }],
        });

        Assert.False(state.Entities.ContainsKey(99));
    }

    [Fact]
    public void ApplyDelta_TileUpdates()
    {
        var state = new ClientGameState();
        state.ApplySnapshot(MakeSnapshot(32, 32));

        state.ApplyDelta(new WorldDeltaMsg
        {
            FromTick = 1,
            ToTick = 2,
            Chunks = [],
            CombatEvents = [],
            EntityUpdates = [],
            TileUpdates = [new TileUpdateMsg { X = 32, Y = 32, TileType = (byte)TileType.Wall, GlyphId = '#', FgColor = 0xAAAAAA, BgColor = 0x111111, LightLevel = 3 }],
        });

        var tile = state.GetTile(32, 32);
        Assert.Equal(TileType.Wall, tile.Type);
        Assert.Equal('#', tile.GlyphId);
        // LightLevel is now computed client-side (player at 32,32 illuminates this tile)
        Assert.True(tile.LightLevel > 0);
    }

    [Fact]
    public void ApplyDelta_PlayerPositionTrackedFromEntity()
    {
        var state = new ClientGameState();
        state.ApplySnapshot(MakeSnapshot(32, 32));

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
        state.ApplySnapshot(MakeSnapshot(32, 32));

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
        state.ApplySnapshot(MakeSnapshot(32, 32));

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
        state.ApplySnapshot(new WorldSnapshotMsg
        {
            WorldTick = 1,
            Chunks = [MakeFloorChunk(0, 0)],
            Entities = [new EntityMsg { Id = 1, X = 32, Y = 32, GlyphId = 64, FgColor = 0xFFFFFF, Health = 100, MaxHealth = 100 }],
            PlayerState = new PlayerStateMsg { Health = 90, MaxHealth = 100 },
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
        state.ApplySnapshot(MakeSnapshot(32, 32));

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
}
