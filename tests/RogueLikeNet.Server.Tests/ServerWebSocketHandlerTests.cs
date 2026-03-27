using System.Net.WebSockets;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;
using RogueLikeNet.Server;

namespace RogueLikeNet.Server.Tests;

public class ServerWebSocketHandlerTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private static byte[] MakeLoginMessage()
    {
        var login = new LoginMsg { PlayerName = "TestHero", ClassId = 0 };
        var payload = NetSerializer.Serialize(login);
        return NetSerializer.WrapMessage(MessageTypes.LoginSend, payload);
    }

    [Fact]
    public async Task HandleConnection_SpawnsPlayerAndSendsSnapshot()
    {
        using var loop = new GameServer(42, _gen);
        var socket = new FakeWebSocket();
        socket.EnqueueReceive(MakeLoginMessage());
        socket.EnqueueClose();

        await ServerWebSocketHandler.HandleConnection(socket, loop);

        Assert.True(socket.SentMessages.Count > 0);
        var first = socket.SentMessages[0];
        var env = NetSerializer.UnwrapMessage(first);
        Assert.Equal(MessageTypes.WorldDelta, env.MessageType);
    }

    [Fact]
    public async Task HandleConnection_ProcessesClientInput()
    {
        using var loop = new GameServer(42, _gen);
        var socket = new FakeWebSocket();

        // Send login first to spawn player
        socket.EnqueueReceive(MakeLoginMessage());

        // Create a valid ClientInput message
        var input = new ClientInputMsg { ActionType = 1, TargetX = 5, TargetY = 3 };
        var payload = NetSerializer.Serialize(input);
        var wrapped = NetSerializer.WrapMessage(MessageTypes.ClientInput, payload);
        socket.EnqueueReceive(wrapped);
        socket.EnqueueClose();

        await ServerWebSocketHandler.HandleConnection(socket, loop);

        // Should have processed without errors and sent snapshot
        Assert.True(socket.SentMessages.Count > 0);
    }

    [Fact]
    public async Task HandleConnection_HandlesChatMessage()
    {
        using var loop = new GameServer(42, _gen);
        var socket = new FakeWebSocket();

        // Send login first
        socket.EnqueueReceive(MakeLoginMessage());

        var chat = new ChatMsg { Text = "Hello" };
        var chatPayload = NetSerializer.Serialize(chat);
        var wrapped = NetSerializer.WrapMessage(MessageTypes.ChatSend, chatPayload);
        socket.EnqueueReceive(wrapped);
        socket.EnqueueClose();

        await ServerWebSocketHandler.HandleConnection(socket, loop);

        // Chat is currently a no-op; should not crash
        Assert.True(socket.SentMessages.Count > 0);
    }

    [Fact]
    public async Task HandleConnection_ClosesSocketOnNormalExit()
    {
        using var loop = new GameServer(42, _gen);
        var socket = new FakeWebSocket();
        socket.EnqueueClose();

        await ServerWebSocketHandler.HandleConnection(socket, loop);

        Assert.Equal(WebSocketState.Closed, socket.State);
    }

    [Fact]
    public async Task HandleConnection_HandlesWebSocketException()
    {
        using var loop = new GameServer(42, _gen);
        var socket = new FakeWebSocket { ThrowOnReceive = true };

        await ServerWebSocketHandler.HandleConnection(socket, loop);

        // Socket state should be Aborted (set by FakeWebSocket before throw)
        Assert.Equal(WebSocketState.Aborted, socket.State);
    }

    [Fact]
    public async Task HandleConnection_HandlesInvalidBinaryData()
    {
        using var loop = new GameServer(42, _gen);
        var socket = new FakeWebSocket();

        // Send invalid/corrupt binary data (before login)
        socket.EnqueueReceive([0xFF, 0xFE, 0x01]);
        socket.EnqueueClose();

        await ServerWebSocketHandler.HandleConnection(socket, loop);

        // Should handle the error in ProcessMessage and continue without crashing
        // No snapshot expected since login was never sent
        Assert.Equal(WebSocketState.Closed, socket.State);
    }

    [Fact]
    public async Task HandleConnection_RemovesConnectionOnClose()
    {
        using var loop = new GameServer(42, _gen);
        var socket = new FakeWebSocket();
        socket.EnqueueClose();

        await ServerWebSocketHandler.HandleConnection(socket, loop);

        // Connection should be removed — adding new connections should still work
        var conn = loop.AddConnection(_ => Task.CompletedTask);
        Assert.NotNull(conn);
    }

    [Fact]
    public async Task HandleConnection_SocketNotOpenSkipsClose()
    {
        using var loop = new GameServer(42, _gen);
        var socket = new FakeWebSocket { ThrowOnReceive = true };

        await ServerWebSocketHandler.HandleConnection(socket, loop);

        // Socket was set to Aborted before throwing — CloseAsync should NOT be called
        Assert.Equal(WebSocketState.Aborted, socket.State);
        Assert.False(socket.CloseAsyncCalled);
    }

    [Fact]
    public async Task HandleConnection_SendSkipsWhenSocketClosed()
    {
        using var loop = new GameServer(42, _gen);
        // Socket starts already closed — the send callback checks state before sending
        var socket = new FakeWebSocket { StartClosed = true };

        await ServerWebSocketHandler.HandleConnection(socket, loop);

        // Send should have been skipped since socket reports as closed
        Assert.Empty(socket.SentMessages);
    }
}

internal class FakeWebSocket : WebSocket
{
    private WebSocketState _state = WebSocketState.Open;
    private readonly Queue<(byte[] Data, WebSocketMessageType Type)> _receiveQueue = new();
    private readonly List<byte[]> _sentMessages = [];

    public override WebSocketCloseStatus? CloseStatus => WebSocketCloseStatus.NormalClosure;
    public override string? CloseStatusDescription => "Normal";
    public override WebSocketState State => StartClosed ? WebSocketState.Closed : _state;
    public override string? SubProtocol => null;

    public List<byte[]> SentMessages => _sentMessages;
    public bool ThrowOnReceive { get; set; }
    public bool CloseAsyncCalled { get; private set; }
    public bool StartClosed { get; init; }

    public void EnqueueReceive(byte[] data, WebSocketMessageType type = WebSocketMessageType.Binary)
        => _receiveQueue.Enqueue((data, type));

    public void EnqueueClose()
        => _receiveQueue.Enqueue(([], WebSocketMessageType.Close));

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken ct)
    {
        if (ThrowOnReceive)
        {
            _state = WebSocketState.Aborted;
            throw new WebSocketException("Connection lost");
        }

        if (_receiveQueue.Count == 0)
        {
            _state = WebSocketState.Closed;
            return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        }

        var (data, type) = _receiveQueue.Dequeue();
        if (type == WebSocketMessageType.Close)
            return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));

        data.CopyTo(buffer.Array!, buffer.Offset);
        return Task.FromResult(new WebSocketReceiveResult(data.Length, WebSocketMessageType.Binary, true));
    }

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType msgType, bool endOfMessage, CancellationToken ct)
    {
        _sentMessages.Add(buffer.ToArray());
        return Task.CompletedTask;
    }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken ct)
    {
        _state = WebSocketState.Closed;
        CloseAsyncCalled = true;
        return Task.CompletedTask;
    }

    public override void Abort() => _state = WebSocketState.Aborted;
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken ct)
    {
        _state = WebSocketState.CloseSent;
        return Task.CompletedTask;
    }
    public override void Dispose() { }
}
