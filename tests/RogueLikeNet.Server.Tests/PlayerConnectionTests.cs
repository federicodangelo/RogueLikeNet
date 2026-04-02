using RogueLikeNet.Protocol.Messages;
using RogueLikeNet.Server;

namespace RogueLikeNet.Server.Tests;

public class PlayerConnectionTests
{
    [Fact]
    public async Task SendAsync_InvokesCallback()
    {
        byte[]? sent = null;
        var conn = new PlayerConnection(1, data =>
        {
            sent = data;
            return Task.CompletedTask;
        });

        await conn.SendAsync([1, 2, 3]);
        Assert.NotNull(sent);
        Assert.Equal(3, sent.Length);
    }

    [Fact]
    public void InputQueue_ThreadSafe()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        var input = new ClientInputMsg { ActionType = 1 };
        conn.InputQueue.Enqueue(input);
        Assert.True(conn.InputQueue.TryDequeue(out var result));
        Assert.Equal(1, result.ActionType);
    }

    [Fact]
    public void PlayerEntityId_DefaultsToNull()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        Assert.Null(conn.PlayerEntityId);
    }

    [Fact]
    public void LastAckedTick_DefaultsToZero()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        Assert.Equal(0, conn.LastAckedTick);
    }

    [Fact]
    public void LastAckedTick_CanBeSet()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        conn.LastAckedTick = 100;
        Assert.Equal(100, conn.LastAckedTick);
    }

    [Fact]
    public void ConnectionId_IsSet()
    {
        var conn = new PlayerConnection(42, _ => Task.CompletedTask);
        Assert.Equal(42, conn.ConnectionId);
    }

    [Fact]
    public void PlayerEntityId_CanBeSet()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        Assert.Null(conn.PlayerEntityId);
        conn.PlayerEntityId = 1;
        Assert.NotNull(conn.PlayerEntityId);
    }

    [Fact]
    public void TrackReceived_IncrementsBytesReceived()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        Assert.Equal(0, conn.BytesReceived);
        conn.TrackReceived(100);
        Assert.Equal(100, conn.BytesReceived);
        conn.TrackReceived(50);
        Assert.Equal(150, conn.BytesReceived);
    }

    [Fact]
    public async Task SendAsync_TracksBytesSent()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        Assert.Equal(0, conn.BytesSent);
        await conn.SendAsync(new byte[100]);
        Assert.Equal(100, conn.BytesSent);
        await conn.SendAsync(new byte[50]);
        Assert.Equal(150, conn.BytesSent);
    }

    [Fact]
    public void LastSentEntities_InitiallyEmpty()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        Assert.Empty(conn.LastSentEntities);
    }

    [Fact]
    public void SentChunkTracker_InitiallyEmpty()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        Assert.Equal(0, conn.SentChunkTracker.Count);
    }

    [Fact]
    public void LastSentHudBytes_DefaultsToNull()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        Assert.Null(conn.LastSentPlayerState);
    }

    [Fact]
    public void LastSentPlayerState_CanBeSetAndCleared()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        conn.LastSentPlayerState = new PlayerStateMsg { Health = 100 };
        Assert.NotNull(conn.LastSentPlayerState);
        Assert.Equal(100, conn.LastSentPlayerState.Health);
        conn.LastSentPlayerState = null;
        Assert.Null(conn.LastSentPlayerState);
    }

    [Fact]
    public void PlayerName_DefaultsToEmpty()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        Assert.Equal("", conn.PlayerName);
    }

    [Fact]
    public void PlayerName_CanBeSet()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        conn.PlayerName = "TestPlayer";
        Assert.Equal("TestPlayer", conn.PlayerName);
    }
}
