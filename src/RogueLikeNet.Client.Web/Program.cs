using System.Runtime.InteropServices.JavaScript;
using Engine.Platform;
using Engine.Platform.Web;
using RogueLikeNet.Client.Core;
using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Protocol.Messages;
using RogueLikeNet.Server;

namespace RogueLikeNet.Client.Web;

public partial class WebMain
{
    private static RogueLikeGame? _game;
    private static IGameServerConnection? _connection;
    private static GameServer? _embeddedServer;

    public static async Task Main()
    {
        var platform = new WebPlatform(
            "RogueLikeNet", 1280, 960,
            new NullMusicProvider(), new NullSfxProvider());

        _game = new RogueLikeGame();
        _game.Initialize(platform);

        _game.StartOfflineRequested += (seed, classId, playerName, genIndex) => OnStartOffline(seed, classId, playerName, genIndex);
        _game.StartOnlineRequested += (classId, playerName) => OnStartOnline(classId, playerName);
        _game.ReturnToMenuRequested += OnReturnToMenu;
        // Web platform cannot quit — QuitRequested is ignored

        await Task.CompletedTask;
    }

    [JSExport]
    public static void RunOneFrame()
    {
        _game?.RunFrame();
    }

    private static async void OnStartOffline(long seed, int classId, string playerName, int generatorIndex)
    {
        _game!.TransitionToConnecting();

        var generator = GeneratorRegistry.Create(generatorIndex, seed);
        _embeddedServer = new GameServer(seed, generator, logWriter: Console.Out);
        _embeddedServer.Start();

        var embeddedConnection = new EmbeddedServerConnection(_embeddedServer);
        _connection = embeddedConnection;
        _game!.SetConnection(_connection);
        await _connection.ConnectAsync("embedded://localhost");
        await _connection.SendLoginAsync(new LoginMsg { ClassId = classId, PlayerName = playerName });

        while (!_game.IsFirstDeltaProcessed)
        {
            await Task.Delay(50);
        }

        _game.TransitionToPlaying();
    }

    private static async void OnStartOnline(int classId, string playerName)
    {
        _game!.TransitionToConnecting();

        try
        {
            var wsConnection = new WebSocketServerConnection();
            _connection = wsConnection;
            _game.SetConnection(_connection);
            await _connection.ConnectAsync("ws://localhost:5090/ws");
            await _connection.SendLoginAsync(new LoginMsg { ClassId = classId, PlayerName = playerName });
            _game.TransitionToPlaying();
        }
        catch (Exception ex)
        {
            _game.ClearConnection();
            _connection = null;
            _game.ShowConnectionError(ex.Message);
        }
    }

    private static void OnReturnToMenu()
    {
        _game!.TransitionToMainMenu();

        _connection?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _connection = null;
        _embeddedServer?.Dispose();
        _embeddedServer = null;
    }

    private sealed class NullMusicProvider : IMusicProvider
    {
        public string CurrentTheme => "";
        public void SetTheme(string theme) { }
        public void Generate(float[] buffer, int frames) { }
    }

    private sealed class NullSfxProvider : ISfxProvider
    {
        public bool TryGetBuffer(string sfx, out float[] buffer) { buffer = []; return false; }
    }
}
