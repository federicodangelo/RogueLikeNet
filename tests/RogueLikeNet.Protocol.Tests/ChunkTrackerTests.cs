using RogueLikeNet.Core.Components;
using RogueLikeNet.Protocol;

namespace RogueLikeNet.Protocol.Tests;

public class ChunkTrackerTests
{
    private static ChunkTracker CreateTracker(int visibleChunks)
    {
        var tracker = new ChunkTracker();
        tracker.UpdateCapacity(visibleChunks);
        return tracker;
    }

    [Fact]
    public void NewTracker_IsEmpty()
    {
        var tracker = new ChunkTracker();
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public void ComputeCapacity_MatchesFormula()
    {
        // visibleChunks=1 → max(9, 1*2)=9
        Assert.Equal(9, ChunkTracker.ComputeCapacity(1));
        // visibleChunks=4 → max(9, 4*2)=9
        Assert.Equal(9, ChunkTracker.ComputeCapacity(4));
        // visibleChunks=5 → max(9, 5*2)=10
        Assert.Equal(10, ChunkTracker.ComputeCapacity(5));
        // visibleChunks=9 → max(9, 9*2)=18
        Assert.Equal(18, ChunkTracker.ComputeCapacity(9));
        // visibleChunks=25 → max(9, 25*2)=50
        Assert.Equal(50, ChunkTracker.ComputeCapacity(25));
        // visibleChunks=100 → max(9, 100*2)=200
        Assert.Equal(200, ChunkTracker.ComputeCapacity(100));
    }

    [Fact]
    public void ComputeCapacity_CapsAtMaxVisibleChunks()
    {
        // Values above MaxVisibleChunks are clamped
        Assert.Equal(400, ChunkTracker.ComputeCapacity(1000));
    }

    [Fact]
    public void ComputeChunkRange_DerivedFromVisibleChunks()
    {
        // 1 visible chunk → range 1
        Assert.Equal(1, ChunkTracker.ComputeChunkRange(1));
        // 4 visible chunks → ceil(sqrt(4))=2, (2+1)/2=1 → range 1
        Assert.Equal(1, ChunkTracker.ComputeChunkRange(4));
        // 9 visible chunks → ceil(sqrt(9))=3, (3+1)/2=2 → range 2
        Assert.Equal(1, ChunkTracker.ComputeChunkRange(9));
        // 16 visible chunks → ceil(sqrt(16))=4, (4+1)/2=2 → range 2
        Assert.Equal(2, ChunkTracker.ComputeChunkRange(16));
        // 25 visible chunks → ceil(sqrt(25))=5, (5+1)/2=3 → range 3
        Assert.Equal(2, ChunkTracker.ComputeChunkRange(25));
    }

    [Fact]
    public void Touch_NewKey_ReturnsTrue()
    {
        var tracker = new ChunkTracker();
        Assert.True(tracker.Touch(100));
        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void Touch_ExistingKey_ReturnsFalse()
    {
        var tracker = new ChunkTracker();
        tracker.Touch(100);
        Assert.False(tracker.Touch(100));
        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void Contains_TracksAddedKeys()
    {
        var tracker = new ChunkTracker();
        Assert.False(tracker.Contains(100));
        tracker.Touch(100);
        Assert.True(tracker.Contains(100));
    }

    [Fact]
    public void Evict_UnderCapacity_ReturnsEmpty()
    {
        var tracker = CreateTracker(9); // capacity=18
        for (int i = 0; i < 10; i++)
            tracker.Touch(i);

        var evicted = tracker.Evict();
        Assert.Empty(evicted);
        Assert.Equal(10, tracker.Count);
    }

    [Fact]
    public void Evict_OverCapacity_RemovesLeastRecentlyUsed()
    {
        var tracker = CreateTracker(9); // capacity=18

        // Add 20 keys (0..19) — exceeds capacity by 2
        for (int i = 0; i < 20; i++)
            tracker.Touch(i);

        var evicted = tracker.Evict();
        Assert.Equal(2, evicted.Length);
        // Oldest keys (0 and 1) should be evicted
        Assert.Equal(0, evicted[0]);
        Assert.Equal(1, evicted[1]);

        Assert.Equal(18, tracker.Count);
        Assert.False(tracker.Contains(0));
        Assert.False(tracker.Contains(1));
        Assert.True(tracker.Contains(2));
        Assert.True(tracker.Contains(19));
    }

    [Fact]
    public void Touch_PromotesExistingKey_EvictsCorrectly()
    {
        var tracker = CreateTracker(9); // capacity=18

        // Add keys 0..17 (exactly at capacity)
        for (int i = 0; i < 18; i++)
            tracker.Touch(i);

        // Promote key 0 (the oldest) to most recent
        tracker.Touch(0);

        // Add 2 more keys to exceed capacity
        tracker.Touch(100);
        tracker.Touch(101);

        var evicted = tracker.Evict();
        Assert.Equal(2, evicted.Length);
        // Keys 1 and 2 should be evicted (0 was promoted)
        Assert.Equal(1, evicted[0]);
        Assert.Equal(2, evicted[1]);

        Assert.True(tracker.Contains(0)); // was promoted
        Assert.False(tracker.Contains(1));
        Assert.False(tracker.Contains(2));
        Assert.True(tracker.Contains(100));
        Assert.True(tracker.Contains(101));
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var tracker = new ChunkTracker();
        for (int i = 0; i < 10; i++)
            tracker.Touch(i);

        tracker.Clear();

        Assert.Equal(0, tracker.Count);
        for (int i = 0; i < 10; i++)
            Assert.False(tracker.Contains(i));
    }

    [Fact]
    public void UpdateCapacity_ChangesMaxCapacity()
    {
        var tracker = CreateTracker(9); // capacity=18
        Assert.Equal(18, tracker.MaxCapacity);

        tracker.UpdateCapacity(25); // capacity=50
        Assert.Equal(50, tracker.MaxCapacity);
    }

    [Fact]
    public void UpdateCapacity_ShrinkTriggersEviction()
    {
        var tracker = CreateTracker(25); // capacity=50

        // Add 30 entries
        for (int i = 0; i < 30; i++)
            tracker.Touch(i);

        // Shrink to capacity=18
        tracker.UpdateCapacity(9);
        var evicted = tracker.Evict();
        Assert.Equal(12, evicted.Length); // 30 - 18 = 12
        Assert.Equal(18, tracker.Count);

        // Evicted should be the oldest 12 keys (0..11)
        for (int i = 0; i < 12; i++)
        {
            Assert.Equal(i, evicted[i]);
            Assert.False(tracker.Contains(i));
        }
        for (int i = 12; i < 30; i++)
            Assert.True(tracker.Contains(i));
    }

    [Fact]
    public void Evict_RepeatedCalls_SecondReturnsEmpty()
    {
        var tracker = CreateTracker(9); // capacity=18

        for (int i = 0; i < 20; i++)
            tracker.Touch(i);

        var evicted1 = tracker.Evict();
        Assert.Equal(2, evicted1.Length);

        var evicted2 = tracker.Evict();
        Assert.Empty(evicted2);
    }

    [Fact]
    public void PackedCoordKeys_WorkCorrectly()
    {
        // Verify that ChunkTracker works with actual packed coords used by the game
        var tracker = new ChunkTracker();

        long key1 = Position.PackCoord(0, 0, Position.DefaultZ);
        long key2 = Position.PackCoord(1, -1, Position.DefaultZ);
        long key3 = Position.PackCoord(-1, 0, Position.DefaultZ);

        Assert.True(tracker.Touch(key1));
        Assert.True(tracker.Touch(key2));
        Assert.True(tracker.Touch(key3));

        Assert.False(tracker.Touch(key1)); // already tracked
        Assert.Equal(3, tracker.Count);
    }
}
