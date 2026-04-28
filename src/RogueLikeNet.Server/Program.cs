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
var serverOptions = ServerRuntimeOptions.FromConfiguration(builder.Configuration);
Console.WriteLine($"Server world seed: {serverOptions.WorldSeed}");
Console.WriteLine($"Save database: {serverOptions.DatabasePath}");
using var saveProvider = new SqliteSaveGameProvider(serverOptions.DatabasePath);
var gameServer = new GameServer(serverOptions.WorldSeed, logWriter: Console.Out, saveProvider: saveProvider);

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
