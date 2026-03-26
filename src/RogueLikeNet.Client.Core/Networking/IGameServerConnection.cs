using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Networking;

/// <summary>
/// Abstraction for server connection. Two implementations:
/// - WebSocketServerConnection (remote server)
/// - EmbeddedServerConnection (in-process, standalone mode)
/// </summary>
public interface IGameServerConnection : IAsyncDisposable
{
    bool IsConnected { get; }
    long BytesSent { get; }
    long BytesReceived { get; }

    Task ConnectAsync(string uri, CancellationToken ct = default);
    Task SendLoginAsync(LoginMsg login, CancellationToken ct = default);
    Task SendInputAsync(ClientInputMsg input, CancellationToken ct = default);
    Task SendChatAsync(string text, CancellationToken ct = default);

    event Action<WorldDeltaMsg>? OnWorldDelta;
    event Action<ChatMsg>? OnChatReceived;
    event Action? OnDisconnected;
}
