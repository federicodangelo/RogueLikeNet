using Engine.Platform;
using Engine.Platform.Sdl;
using RogueLikeNet.Client.Core;
using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Server;

namespace RogueLikeNet.Client.Desktop;

public class Program
{
    private static RogueLikeGame? _game;
    private static IGameServerConnection? _connection;
    private static GameLoop? _embeddedServer;
    private static bool _running = true;

    [STAThread]
    public static void Main(string[] args)
    {
        using var platform = new SdlPlatform(
            "RogueLikeNet", 1280, 960,
            new NullMusicProvider(), new NullSfxProvider());

        _game = new RogueLikeGame();
        _game.Initialize(platform);

        _game.StartOfflineRequested += seed => OnStartOffline(seed);
        _game.StartOnlineRequested += seed => OnStartOnline(seed);
        _game.ReturnToMenuRequested += OnReturnToMenu;
        _game.QuitRequested += () => _running = false;

        while (_running && !platform.InputManager.QuitRequested)
        {
            _game.RunFrame();
        }

        CleanupConnection();
        _game.Dispose();
    }

    private static async void OnStartOffline(long seed)
    {
        _embeddedServer = new GameLoop(seed);
        _embeddedServer.Start();

        var embeddedConnection = new EmbeddedServerConnection(_embeddedServer);
        _connection = embeddedConnection;
        _game!.SetConnection(_connection);
        await _connection.ConnectAsync("embedded://localhost");
        _game.TransitionToPlaying();
    }

    private static async void OnStartOnline(long seed)
    {
        _game!.TransitionToConnecting();

        try
        {
            var wsConnection = new WebSocketServerConnection();
            _connection = wsConnection;
            _game.SetConnection(_connection);
            await _connection.ConnectAsync("ws://localhost:5000/ws");
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
        CleanupConnection();
    }

    private static void CleanupConnection()
    {
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
