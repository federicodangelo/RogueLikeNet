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

    Task ConnectAsync(string uri, CancellationToken ct = default);
    Task SendInputAsync(ClientInputMsg input, CancellationToken ct = default);

    event Action<WorldSnapshotMsg>? OnWorldSnapshot;
    event Action<WorldDeltaMsg>? OnWorldDelta;
    event Action? OnDisconnected;
}
