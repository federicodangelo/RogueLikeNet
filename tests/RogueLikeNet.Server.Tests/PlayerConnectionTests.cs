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
    public void PlayerEntity_DefaultsToNull()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        Assert.Null(conn.PlayerEntity);
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
    public void PlayerEntity_CanBeSet()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        Assert.Null(conn.PlayerEntity);
        conn.PlayerEntity = new Arch.Core.Entity();
        Assert.NotNull(conn.PlayerEntity);
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
    public void SentChunkKeys_InitiallyEmpty()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        Assert.Empty(conn.SentChunkKeys);
    }

    [Fact]
    public void LastSentHudBytes_DefaultsToNull()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        Assert.Null(conn.LastSentHudBytes);
    }

    [Fact]
    public void LastSentHudBytes_CanBeSetAndCleared()
    {
        var conn = new PlayerConnection(1, _ => Task.CompletedTask);
        conn.LastSentHudBytes = new byte[] { 1, 2, 3 };
        Assert.NotNull(conn.LastSentHudBytes);
        Assert.Equal(3, conn.LastSentHudBytes.Length);
        conn.LastSentHudBytes = null;
        Assert.Null(conn.LastSentHudBytes);
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
