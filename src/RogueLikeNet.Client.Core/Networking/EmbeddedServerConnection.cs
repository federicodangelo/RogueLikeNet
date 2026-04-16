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
    public event Action<SaveGameResponseMsg>? OnSaveGameResponse;
    public event Action<LoginResponseMsg>? OnLoginResponse;
    public event Action? OnDisconnected;

    public EmbeddedServerConnection(GameServer gameServer)
    {
        _gameServer = gameServer;
    }

    public Task ConnectAsync(string uri, CancellationToken ct = default)
    {
        // Register with the embedded game loop using a callback that deserializes and dispatches
        var conn = _gameServer.AddConnection(
            ProcessServerData,
            async () => { _connected = false; }
        );
        _connectionId = conn.ConnectionId;
        _connected = true;

        return Task.CompletedTask;
    }

    public Task ReconnectAsync(CancellationToken ct = default)
    {
        var conn = _gameServer.AddConnection(
            ProcessServerData,
            async () => { _connected = false; }
        );
        _connectionId = conn.ConnectionId;
        _connected = true;

        return Task.CompletedTask;
    }

    public Task SendLoginAsync(LoginMsg login, CancellationToken ct = default)
    {
        if (!_connected) return Task.CompletedTask;
        _gameServer.AuthenticatePlayer(_connectionId, login.PlayerName, login.Password);
        return Task.CompletedTask;
    }

    public Task SendClassSelectAsync(ClassSelectMsg msg, CancellationToken ct = default)
    {
        if (!_connected) return Task.CompletedTask;
        _gameServer.SelectClassForConnection(_connectionId, msg.ClassId);
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

    public Task SendViewportInfoAsync(ViewportInfoMsg info, CancellationToken ct = default)
    {
        if (!_connected) return Task.CompletedTask;
        _gameServer.UpdateVisibleChunks(_connectionId, info.VisibleChunks);
        return Task.CompletedTask;
    }

    public async Task SendSaveGameCommandAsync(SaveGameCommandMsg cmd, CancellationToken ct = default)
    {
        if (!_connected) return;
        // Route through the server's command queue for thread-safety
        await _gameServer.HandleSaveGameCommand(_connectionId, cmd);
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

                case MessageTypes.SaveGameResponse:
                    var saveResponse = NetSerializer.Deserialize<SaveGameResponseMsg>(envelope.Payload);
                    OnSaveGameResponse?.Invoke(saveResponse);
                    break;

                case MessageTypes.LoginResponse:
                    var loginResponse = NetSerializer.Deserialize<LoginResponseMsg>(envelope.Payload);
                    OnLoginResponse?.Invoke(loginResponse);
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
