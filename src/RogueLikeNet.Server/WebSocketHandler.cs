using System.Net.WebSockets;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Server;

/// <summary>
/// ASP.NET Core WebSocket middleware that handles game connections.
/// </summary>
public static class WebSocketHandler
{
    public static async Task HandleConnection(WebSocket socket, GameLoop gameLoop)
    {
        // Create connection with send function
        var conn = gameLoop.AddConnection(async data =>
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(
                    new ArraySegment<byte>(data),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    CancellationToken.None);
            }
        });

        Console.WriteLine($"[Server] Player {conn.ConnectionId} connected ({gameLoop.ConnectionCount} online)");

        try
        {
            // Spawn player
            await gameLoop.SpawnPlayerForConnection(conn.ConnectionId);

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
                        CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Binary && ms.Length > 0)
                {
                    conn.TrackReceived(ms.Length);
                    if (ProcessMessage(conn, ms.ToArray(), gameLoop))
                        break;

                }

                ms.SetLength(0); // Clear for next message
            }
        }
        catch (WebSocketException)
        {
            // Client disconnected unexpectedly
        }
        finally
        {
            gameLoop.RemoveConnection(conn.ConnectionId);
            Console.WriteLine($"[Server] Player {conn.ConnectionId} disconnected ({gameLoop.ConnectionCount} online)");
            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Server closing connection",
                    CancellationToken.None);
            }
        }
    }

    private static bool ProcessMessage(PlayerConnection conn, byte[] data, GameLoop gameLoop)
    {
        try
        {
            var envelope = NetSerializer.UnwrapMessage(data);
            switch (envelope.MessageType)
            {
                case MessageTypes.ClientInput:
                    var input = NetSerializer.Deserialize<ClientInputMsg>(envelope.Payload);
                    gameLoop.EnqueueInput(conn.ConnectionId, input);
                    break;

                case MessageTypes.ChatSend:
                    var chat = NetSerializer.Deserialize<ChatMsg>(envelope.Payload);
                    _ = gameLoop.BroadcastChat(conn.ConnectionId, chat.Text);
                    break;

                default:
                    Console.Error.WriteLine($"Unknown message type from {conn.ConnectionId}: {envelope.MessageType}");
                    return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error processing message from {conn.ConnectionId}: {ex.Message}");
            return false;
        }
    }
}
