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
    private long _bytesSent;
    private long _bytesReceived;

    public bool IsConnected => _socket?.State == WebSocketState.Open;
    public long BytesSent => Interlocked.Read(ref _bytesSent);
    public long BytesReceived => Interlocked.Read(ref _bytesReceived);

    public event Action<WorldDeltaMsg>? OnWorldDelta;
    public event Action<ChatMsg>? OnChatReceived;
    public event Action<SaveGameResponseMsg>? OnSaveGameResponse;
    public event Action<LoginResponseMsg>? OnLoginResponse;
    public event Action? OnDisconnected;

    private string _uri = "";

    public async Task ConnectAsync(string uri, CancellationToken ct = default)
    {
        _uri = uri;
        _socket = new ClientWebSocket();
        await _socket.ConnectAsync(new Uri(uri), ct);

        _readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _readTask = Task.Run(() => ReadLoop(_readCts.Token));
    }

    public async Task ReconnectAsync(CancellationToken ct = default)
    {
        if (_socket != null)
        {
            await DisposeAsync();
        }
        await ConnectAsync(uri: _uri, ct);
    }

    public async Task SendInputAsync(ClientInputMsg input, CancellationToken ct = default)
    {
        if (_socket?.State != WebSocketState.Open) return;

        var payload = NetSerializer.Serialize(input);
        var data = NetSerializer.WrapMessage(MessageTypes.ClientInput, payload);
        Interlocked.Add(ref _bytesSent, data.Length);
        await _socket.SendAsync(
            new ArraySegment<byte>(data),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            ct);
    }

    public async Task SendLoginAsync(LoginMsg login, CancellationToken ct = default)
    {
        if (_socket?.State != WebSocketState.Open) return;

        var payload = NetSerializer.Serialize(login);
        var data = NetSerializer.WrapMessage(MessageTypes.LoginSend, payload);
        Interlocked.Add(ref _bytesSent, data.Length);
        await _socket.SendAsync(
            new ArraySegment<byte>(data),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            ct);
    }

    public async Task SendClassSelectAsync(ClassSelectMsg msg, CancellationToken ct = default)
    {
        if (_socket?.State != WebSocketState.Open) return;

        var payload = NetSerializer.Serialize(msg);
        var data = NetSerializer.WrapMessage(MessageTypes.ClassSelect, payload);
        Interlocked.Add(ref _bytesSent, data.Length);
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
        Interlocked.Add(ref _bytesSent, data.Length);
        await _socket.SendAsync(
            new ArraySegment<byte>(data),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            ct);
    }

    public async Task SendViewportInfoAsync(ViewportInfoMsg info, CancellationToken ct = default)
    {
        if (_socket?.State != WebSocketState.Open) return;

        var payload = NetSerializer.Serialize(info);
        var data = NetSerializer.WrapMessage(MessageTypes.ViewportInfo, payload);
        Interlocked.Add(ref _bytesSent, data.Length);
        await _socket.SendAsync(
            new ArraySegment<byte>(data),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            ct);
    }

    public async Task SendSaveGameCommandAsync(SaveGameCommandMsg cmd, CancellationToken ct = default)
    {
        if (_socket?.State != WebSocketState.Open) return;

        var payload = NetSerializer.Serialize(cmd);
        var data = NetSerializer.WrapMessage(MessageTypes.SaveGameCommand, payload);
        Interlocked.Add(ref _bytesSent, data.Length);
        await _socket.SendAsync(
            new ArraySegment<byte>(data),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            ct);
    }

    private async Task ReadLoop(CancellationToken ct)
    {
        var buffer = new byte[65536];
        using var ms = new MemoryStream();
        try
        {
            while (!ct.IsCancellationRequested && _socket?.State == WebSocketState.Open)
            {
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
                {
                    Interlocked.Add(ref _bytesReceived, ms.Length);
                    ProcessMessage(ms.ToArray());
                }
                ms.SetLength(0); // Clear for next message
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            OnDisconnected?.Invoke();
        }
    }

    private bool ProcessMessage(byte[] data)
    {
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
                    var saveResp = NetSerializer.Deserialize<SaveGameResponseMsg>(envelope.Payload);
                    OnSaveGameResponse?.Invoke(saveResp);
                    break;

                case MessageTypes.LoginResponse:
                    var loginResp = NetSerializer.Deserialize<LoginResponseMsg>(envelope.Payload);
                    OnLoginResponse?.Invoke(loginResp);
                    break;

                default:
                    Console.Error.WriteLine($"Unknown message type received: {envelope.MessageType}");
                    return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error processing server message: {ex.Message}");
            return false;
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
