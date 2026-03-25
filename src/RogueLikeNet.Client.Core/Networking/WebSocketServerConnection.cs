using System.Net.WebSockets;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Networking;

/// <summary>
/// Connects to a remote game server via WebSocket.
/// </summary>
public class WebSocketServerConnection : IGameServerConnection
{
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    public bool IsConnected => _socket?.State == WebSocketState.Open;

    public event Action<WorldSnapshotMsg>? OnWorldSnapshot;
    public event Action<WorldDeltaMsg>? OnWorldDelta;
    public event Action<ChatMsg>? OnChatReceived;
    public event Action? OnDisconnected;

    public async Task ConnectAsync(string uri, CancellationToken ct = default)
    {
        _socket = new ClientWebSocket();
        await _socket.ConnectAsync(new Uri(uri), ct);

        _readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _readTask = Task.Run(() => ReadLoop(_readCts.Token));
    }

    public async Task SendInputAsync(ClientInputMsg input, CancellationToken ct = default)
    {
        if (_socket?.State != WebSocketState.Open) return;

        var payload = NetSerializer.Serialize(input);
        var data = NetSerializer.WrapMessage(MessageTypes.ClientInput, payload);
        await _socket.SendAsync(
            new ArraySegment<byte>(data),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            ct);
    }

    public async Task SendChatAsync(string text, CancellationToken ct = default)
    {
        if (_socket?.State != WebSocketState.Open) return;

        var msg = new ChatMsg { Text = text };
        var payload = NetSerializer.Serialize(msg);
        var data = NetSerializer.WrapMessage(MessageTypes.ChatSend, payload);
        await _socket.SendAsync(
            new ArraySegment<byte>(data),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            ct);
    }

    private async Task ReadLoop(CancellationToken ct)
    {
        var buffer = new byte[65536];
        try
        {
            while (!ct.IsCancellationRequested && _socket?.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Binary && ms.Length > 0)
                    ProcessMessage(ms.ToArray());
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            OnDisconnected?.Invoke();
        }
    }

    private void ProcessMessage(byte[] data)
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
            Console.Error.WriteLine($"Error processing server message: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _readCts?.Cancel();
        if (_socket?.State == WebSocketState.Open)
        {
            try
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
            }
            catch { }
        }
        _socket?.Dispose();
        _readCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
