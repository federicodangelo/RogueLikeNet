using RogueLikeNet.Server;
using RogueLikeNet.Server.Persistence;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Create game loop with SQLite persistence
long worldSeed = 12345; // TODO: configurable
var saveProvider = new SqliteSaveGameProvider("game.db");
var gameServer = new GameServer(worldSeed, logWriter: Console.Out, saveProvider: saveProvider);

// Auto-load last save or create a new default slot
var slots = saveProvider.ListSaveSlots();
if (slots.Count > 0)
{
    var latest = slots.OrderByDescending(s => s.LastSavedAt).First();
    gameServer.LoadSaveSlot(latest.SlotId);
}
else
{
    gameServer.StartNewGame("Default World", worldSeed, "overworld");
}

gameServer.Start();

app.UseWebSockets();

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var socket = await context.WebSockets.AcceptWebSocketAsync();
        await ServerWebSocketHandler.HandleConnection(socket, gameServer, Console.Out);
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

