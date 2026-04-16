using RogueLikeNet.Core.Data;
using RogueLikeNet.Server;
using RogueLikeNet.Server.Persistence;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Load game data from JSON files
var dataDir = DataDirectory.FindOrThrow();
GameData.Instance = DataLoader.Load(dataDir);
GameData.Instance.LogLoadedData(Console.Out);

// Create game loop with SQLite persistence
long worldSeed = 12345; // TODO: configurable
using var saveProvider = new SqliteSaveGameProvider("game.db");
var gameServer = new GameServer(worldSeed, logWriter: Console.Out, saveProvider: saveProvider);

// Auto-load last save or create a new default slot
gameServer.InitializeFromSaveProvider();

gameServer.Start();

app.UseWebSockets();

var serverShutdownCts = new CancellationTokenSource();

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await ServerWebSocketHandler.HandleConnection(socket, gameServer, Console.Out, serverShutdownCts.Token);
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
    serverShutdownCts.Cancel();
});

app.Run();
