using System.Runtime.InteropServices.JavaScript;
using Engine.Platform;
using Engine.Platform.Web;
using RogueLikeNet.Client.Core;
using RogueLikeNet.Client.Core.Networking;

namespace RogueLikeNet.Client.Web;

public partial class WebMain
{
    private static RogueLikeGame? _game;
    private static IGameServerConnection? _connection;

    public static async Task Main()
    {
        var platform = new WebPlatform(
            "RogueLikeNet", 960, 800,
            new NullMusicProvider(), new NullSfxProvider());

        _game = new RogueLikeGame();
        _game.Initialize(platform);

        _game.StartOfflineRequested += OnStartOffline;
        _game.StartOnlineRequested += OnStartOnline;
        _game.ReturnToMenuRequested += OnReturnToMenu;
        // Web platform cannot quit — QuitRequested is ignored

        await Task.CompletedTask;
    }

    [JSExport]
    public static void RunOneFrame()
    {
        _game?.RunFrame();
    }

    private static async void OnStartOffline()
    {
        _connection = new LocalGameConnection(42);
        _game!.SetConnection(_connection);
        await _connection.ConnectAsync("local://");
        _game.TransitionToPlaying();
    }

    private static async void OnStartOnline()
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

        _connection?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _connection = null;
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
