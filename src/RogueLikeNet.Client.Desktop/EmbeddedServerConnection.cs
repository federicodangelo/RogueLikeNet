using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;
using RogueLikeNet.Server;

namespace RogueLikeNet.Client.Desktop;

/// <summary>
/// In-process connection to an embedded GameLoop for standalone/offline mode.
/// Bypasses network serialization by sending messages directly through callbacks.
/// </summary>
public class EmbeddedServerConnection : IGameServerConnection
{
    private readonly GameLoop _gameLoop;
    private long _connectionId;
    private bool _connected;

    public bool IsConnected => _connected;

    public event Action<WorldSnapshotMsg>? OnWorldSnapshot;
    public event Action<WorldDeltaMsg>? OnWorldDelta;
    public event Action<ChatMsg>? OnChatReceived;
    public event Action? OnDisconnected;

    public EmbeddedServerConnection(GameLoop gameLoop)
    {
        _gameLoop = gameLoop;
    }

    public async Task ConnectAsync(string uri, CancellationToken ct = default)
    {
        // Register with the embedded game loop using a callback that deserializes and dispatches
        var conn = _gameLoop.AddConnection(ProcessServerData);
        _connectionId = conn.ConnectionId;
        _connected = true;

        // Spawn the player
        await _gameLoop.SpawnPlayerForConnection(_connectionId);
    }

    public Task SendInputAsync(ClientInputMsg input, CancellationToken ct = default)
    {
        if (!_connected) return Task.CompletedTask;
        _gameLoop.EnqueueInput(_connectionId, input);
        return Task.CompletedTask;
    }

    public async Task SendChatAsync(string text, CancellationToken ct = default)
    {
        if (!_connected) return;
        await _gameLoop.BroadcastChat(_connectionId, text);
    }

    private Task ProcessServerData(byte[] data)
    {
        try
        {
            var envelope = NetSerializer.UnwrapMessage(data);
            switch (envelope.MessageType)
            {
                case MessageTypes.WorldSnapshot:
                    var snapshot = NetSerializer.Deserialize<WorldSnapshotMsg>(envelope.Payload);
                    OnWorldSnapshot?.Invoke(snapshot);
                    break;

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
            _gameLoop.RemoveConnection(_connectionId);
            OnDisconnected?.Invoke();
        }
        return ValueTask.CompletedTask;
    }
}
