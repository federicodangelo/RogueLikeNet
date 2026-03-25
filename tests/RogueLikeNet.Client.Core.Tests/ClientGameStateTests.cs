using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Tests;

public class ClientGameStateTests
{
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
                    TileLightLevels = new int[64 * 64],
                }
            ],
            Entities = [],
        });

        Assert.Single(state.Chunks);
    }
}
