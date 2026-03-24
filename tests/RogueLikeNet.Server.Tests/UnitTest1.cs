using RogueLikeNet.Core.Components;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;
using RogueLikeNet.Server;

namespace RogueLikeNet.Server.Tests;

public class GameLoopTests
{
    [Fact]
    public void GameLoop_StartsAndStops()
    {
        using var loop = new GameLoop(42);
        loop.Start();
        Assert.True(loop.IsRunning);
        loop.Dispose();
    }

    [Fact]
    public void AddConnection_ReturnsValidConnection()
    {
        using var loop = new GameLoop(42);
        var conn = loop.AddConnection(_ => Task.CompletedTask);
        Assert.NotNull(conn);
        Assert.True(conn.ConnectionId > 0);
    }

    [Fact]
    public async Task SpawnPlayerForConnection_CreatesEntity()
    {
        using var loop = new GameLoop(42);
        var conn = loop.AddConnection(_ => Task.CompletedTask);
        await loop.SpawnPlayerForConnection(conn.ConnectionId);
        Assert.NotNull(conn.PlayerEntity);
        Assert.True(loop.Engine.EcsWorld.IsAlive(conn.PlayerEntity.Value));
    }

    [Fact]
    public void RemoveConnection_DestroysEntity()
    {
        using var loop = new GameLoop(42);
        var conn = loop.AddConnection(_ => Task.CompletedTask);
        loop.SpawnPlayerForConnection(conn.ConnectionId).Wait();

        var entity = conn.PlayerEntity!.Value;
        loop.RemoveConnection(conn.ConnectionId);

        Assert.False(loop.Engine.EcsWorld.IsAlive(entity));
    }

    [Fact]
    public void EnqueueInput_QueuesCorrectly()
    {
        using var loop = new GameLoop(42);
        var conn = loop.AddConnection(_ => Task.CompletedTask);

        var input = new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 };
        loop.EnqueueInput(conn.ConnectionId, input);

        Assert.True(conn.InputQueue.TryDequeue(out var dequeued));
        Assert.Equal(ActionTypes.Move, dequeued.ActionType);
        Assert.Equal(1, dequeued.TargetX);
    }

    [Fact]
    public async Task SpawnPlayer_SendsSnapshot()
    {
        byte[]? receivedData = null;
        using var loop = new GameLoop(42);
        var conn = loop.AddConnection(data =>
        {
            receivedData = data;
            return Task.CompletedTask;
        });

        await loop.SpawnPlayerForConnection(conn.ConnectionId);

        Assert.NotNull(receivedData);
        var envelope = NetSerializer.UnwrapMessage(receivedData);
        Assert.Equal(MessageTypes.WorldSnapshot, envelope.MessageType);

        var snapshot = NetSerializer.Deserialize<WorldSnapshotMsg>(envelope.Payload);
        Assert.True(snapshot.Chunks.Length > 0);
    }
}

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
}
