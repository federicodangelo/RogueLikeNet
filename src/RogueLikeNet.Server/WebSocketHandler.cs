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

        try
        {
            // Spawn player
            await gameLoop.SpawnPlayerForConnection(conn.ConnectionId);

            // Read loop
            var buffer = new byte[4096];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Binary && result.Count > 0)
                {
                    var data = new byte[result.Count];
                    Array.Copy(buffer, data, result.Count);
                    ProcessMessage(conn, data, gameLoop);
                }
            }
        }
        catch (WebSocketException)
        {
            // Client disconnected unexpectedly
        }
        finally
        {
            gameLoop.RemoveConnection(conn.ConnectionId);
            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Server closing connection",
                    CancellationToken.None);
            }
        }
    }

    private static void ProcessMessage(PlayerConnection conn, byte[] data, GameLoop gameLoop)
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
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error processing message from {conn.ConnectionId}: {ex.Message}");
        }
    }
}
