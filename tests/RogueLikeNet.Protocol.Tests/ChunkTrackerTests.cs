using RogueLikeNet.Core.Components;
using RogueLikeNet.Protocol;

namespace RogueLikeNet.Protocol.Tests;

public class ChunkTrackerTests
{
    [Fact]
    public void NewTracker_IsEmpty()
    {
        var tracker = new ChunkTracker(1);
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public void ComputeCapacity_MatchesFormula()
    {
        // chunkRange=1 → side=3, capacity=18
        Assert.Equal(18, ChunkTracker.ComputeCapacity(1));
        // chunkRange=2 → side=5, capacity=50
        Assert.Equal(50, ChunkTracker.ComputeCapacity(2));
        // chunkRange=3 → side=7, capacity=98
        Assert.Equal(98, ChunkTracker.ComputeCapacity(3));
    }

    [Fact]
    public void Touch_NewKey_ReturnsTrue()
    {
        var tracker = new ChunkTracker(1);
        Assert.True(tracker.Touch(100));
        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void Touch_ExistingKey_ReturnsFalse()
    {
        var tracker = new ChunkTracker(1);
        tracker.Touch(100);
        Assert.False(tracker.Touch(100));
        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void Contains_TracksAddedKeys()
    {
        var tracker = new ChunkTracker(1);
        Assert.False(tracker.Contains(100));
        tracker.Touch(100);
        Assert.True(tracker.Contains(100));
    }

    [Fact]
    public void Evict_UnderCapacity_ReturnsEmpty()
    {
        var tracker = new ChunkTracker(1); // capacity=18
        for (int i = 0; i < 10; i++)
            tracker.Touch(i);

        var evicted = tracker.Evict();
        Assert.Empty(evicted);
        Assert.Equal(10, tracker.Count);
    }

    [Fact]
    public void Evict_OverCapacity_RemovesLeastRecentlyUsed()
    {
        var tracker = new ChunkTracker(1); // capacity=18

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
        var tracker = new ChunkTracker(1); // capacity=18

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
        var tracker = new ChunkTracker(1);
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
        var tracker = new ChunkTracker(1); // capacity=18
        Assert.Equal(18, tracker.MaxCapacity);

        tracker.UpdateCapacity(2); // capacity=50
        Assert.Equal(50, tracker.MaxCapacity);
    }

    [Fact]
    public void UpdateCapacity_ShrinkTriggersEviction()
    {
        var tracker = new ChunkTracker(2); // capacity=50

        // Add 30 entries
        for (int i = 0; i < 30; i++)
            tracker.Touch(i);

        // Shrink to capacity=18
        tracker.UpdateCapacity(1);
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
        var tracker = new ChunkTracker(1); // capacity=18

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
        var tracker = new ChunkTracker(1);

        long key1 = Position.PackCoord(0, 0);
        long key2 = Position.PackCoord(1, -1);
        long key3 = Position.PackCoord(-1, 0);

        Assert.True(tracker.Touch(key1));
        Assert.True(tracker.Touch(key2));
        Assert.True(tracker.Touch(key3));

        Assert.False(tracker.Touch(key1)); // already tracked
        Assert.Equal(3, tracker.Count);
    }
}
