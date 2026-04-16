using System.Net.WebSockets;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Server;

/// <summary>
/// ASP.NET Core WebSocket middleware that handles game connections.
/// </summary>
public static class ServerWebSocketHandler
{
    public static async Task HandleConnection(WebSocket socket, GameServer gameServer, TextWriter? logWriter = null, CancellationToken serverShutdownCancellationToken = default)
    {
        logWriter ??= TextWriter.Null;

        var receiveCts = new CancellationTokenSource();

        var receiveOrShutdownCts = CancellationTokenSource.CreateLinkedTokenSource(receiveCts.Token, serverShutdownCancellationToken);

        // Create connection with send function
        var conn = gameServer.AddConnection(
            // send
            async data =>
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(
                        new ArraySegment<byte>(data),
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        CancellationToken.None);
                }
            },
            // close
            async () =>
            {
                // Just cancel the receive loop; the finally block will handle cleanup and socket closing
                receiveOrShutdownCts.Cancel();
            }
        );

        logWriter.WriteLine($"[Server] Player {conn.ConnectionId} connected ({gameServer.ConnectionCount} online)");

        try
        {
            // Player will be spawned when LoginMsg is received

            // Read loop
            var buffer = new byte[65536];
            using var ms = new MemoryStream();
            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        receiveOrShutdownCts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Binary && ms.Length > 0)
                {
                    conn.TrackReceived(ms.Length);
                    if (!ProcessMessage(conn, ms.ToArray(), gameServer, logWriter))
                        break;

                }

                ms.SetLength(0); // Clear for next message
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation when closing connection
        }
        catch (WebSocketException)
        {
            // Client disconnected unexpectedly
        }
        finally
        {
            gameServer.RemoveConnection(conn.ConnectionId);
            logWriter.WriteLine($"[Server] Player {conn.ConnectionId} disconnected ({gameServer.ConnectionCount} online)");
            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Server closing connection",
                    CancellationToken.None);
            }
        }
    }

    private static bool ProcessMessage(PlayerConnection conn, byte[] data, GameServer gameServer, TextWriter logWriter)
    {
        try
        {
            var envelope = NetSerializer.UnwrapMessage(data);
            switch (envelope.MessageType)
            {
                case MessageTypes.LoginSend:
                    if (conn.PlayerEntityId == null && !conn.IsAuthenticated)
                    {
                        var login = NetSerializer.Deserialize<LoginMsg>(envelope.Payload);
                        gameServer.AuthenticatePlayer(conn.ConnectionId, login.PlayerName, login.Password);
                    }
                    else
                    {
                        logWriter.WriteLine($"Player {conn.ConnectionId} attempted to login but is already logged in");
                        return false;
                    }
                    break;

                case MessageTypes.ClassSelect:
                    if (conn.IsAuthenticated && conn.PlayerEntityId == null)
                    {
                        var classSelect = NetSerializer.Deserialize<ClassSelectMsg>(envelope.Payload);
                        gameServer.SelectClassForConnection(conn.ConnectionId, classSelect.ClassId);
                    }
                    else
                    {
                        logWriter.WriteLine($"Player {conn.ConnectionId} sent class select in invalid state");
                        return false;
                    }
                    break;

                case MessageTypes.ClientInput:
                    var input = NetSerializer.Deserialize<ClientInputMsg>(envelope.Payload);
                    gameServer.EnqueueInput(conn.ConnectionId, input);
                    break;

                case MessageTypes.ChatSend:
                    var chat = NetSerializer.Deserialize<ChatMsg>(envelope.Payload);
                    gameServer.BroadcastChat(conn.ConnectionId, chat.Text);
                    break;

                case MessageTypes.ViewportInfo:
                    var viewport = NetSerializer.Deserialize<ViewportInfoMsg>(envelope.Payload);
                    gameServer.UpdateVisibleChunks(conn.ConnectionId, viewport.VisibleChunks);
                    break;

                case MessageTypes.SaveGameCommand:
                    var saveCmd = NetSerializer.Deserialize<SaveGameCommandMsg>(envelope.Payload);
                    gameServer.HandleSaveGameCommand(conn.ConnectionId, saveCmd);
                    break;

                default:
                    logWriter.WriteLine($"Unknown message type from {conn.ConnectionId}: {envelope.MessageType}");
                    return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            logWriter.WriteLine($"Error processing message from {conn.ConnectionId}: {ex.Message}");
            return false;
        }
    }
}
