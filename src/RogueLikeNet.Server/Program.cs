using RogueLikeNet.Server;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Create game loop
long worldSeed = 12345; // TODO: configurable
var gameServer = new GameServer(worldSeed);
gameServer.Start();

app.UseWebSockets();

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var socket = await context.WebSockets.AcceptWebSocketAsync();
        await WebSocketHandler.HandleConnection(socket, gameServer);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.MapGet("/", () => "RogueLikeNet Server is running");

app.Lifetime.ApplicationStopping.Register(() =>
{
    gameServer.Dispose();
});

app.Run();

