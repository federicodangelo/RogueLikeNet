using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;
using RogueLikeNet.Server;

namespace RogueLikeNet.Client.Core.Networking;

/// <summary>
/// In-process connection to an embedded GameServer for standalone/offline mode.
/// Bypasses network serialization by sending messages directly through callbacks.
/// </summary>
public class EmbeddedServerConnection : IGameServerConnection
{
    private readonly GameServer _gameServer;
    private long _connectionId;
    private bool _connected;
    private long _bytesReceived;

    public bool IsConnected => _connected;
    public long BytesSent => 0;
    public long BytesReceived => Interlocked.Read(ref _bytesReceived);

    public event Action<WorldDeltaMsg>? OnWorldDelta;
    public event Action<ChatMsg>? OnChatReceived;
    public event Action? OnDisconnected;

    public EmbeddedServerConnection(GameServer gameServer)
    {
        _gameServer = gameServer;
    }

    public Task ConnectAsync(string uri, CancellationToken ct = default)
    {
        // Register with the embedded game loop using a callback that deserializes and dispatches
        var conn = _gameServer.AddConnection(ProcessServerData);
        _connectionId = conn.ConnectionId;
        _connected = true;

        return Task.CompletedTask;
    }

    public Task SendLoginAsync(LoginMsg login, CancellationToken ct = default)
    {
        if (!_connected) return Task.CompletedTask;
        _gameServer.SpawnPlayerForConnection(_connectionId, login.ClassId, login.PlayerName);
        return Task.CompletedTask;
    }

    public Task SendInputAsync(ClientInputMsg input, CancellationToken ct = default)
    {
        if (!_connected) return Task.CompletedTask;
        _gameServer.EnqueueInput(_connectionId, input);
        return Task.CompletedTask;
    }

    public Task SendChatAsync(string text, CancellationToken ct = default)
    {
        if (!_connected) return Task.CompletedTask;
        _gameServer.BroadcastChat(_connectionId, text);
        return Task.CompletedTask;
    }

    private Task ProcessServerData(byte[] data)
    {
        Interlocked.Add(ref _bytesReceived, data.Length);
        try
        {
            var envelope = NetSerializer.UnwrapMessage(data);
            switch (envelope.MessageType)
            {
                case MessageTypes.WorldDelta:
                    var delta = NetSerializer.Deserialize<WorldDeltaMsg>(envelope.Payload);
                    OnWorldDelta?.Invoke(delta);
                    break;

                case MessageTypes.ChatReceive:
                    var chat = NetSerializer.Deserialize<ChatMsg>(envelope.Payload);
                    OnChatReceived?.Invoke(chat);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Embedded connection error: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_connected)
        {
            _connected = false;
            _gameServer.RemoveConnection(_connectionId);
            OnDisconnected?.Invoke();
        }
        return ValueTask.CompletedTask;
    }
}
