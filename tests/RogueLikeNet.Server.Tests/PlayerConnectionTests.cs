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
}
