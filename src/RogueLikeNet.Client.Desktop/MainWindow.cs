using Avalonia.Controls;
using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Server;

namespace RogueLikeNet.Client.Desktop;

public class MainWindow : Window
{
    private readonly GameRenderControl _gameControl;
    private IGameServerConnection? _connection;
    private GameLoop? _embeddedServer;

    public MainWindow()
    {
        Title = "RogueLikeNet";
        Width = 960;
        Height = 800;
        CanResize = true;

        _gameControl = new GameRenderControl();
        Content = _gameControl;

        _gameControl.StartOfflineRequested += OnStartOffline;
        _gameControl.StartOnlineRequested += OnStartOnline;
        _gameControl.ReturnToMenuRequested += OnReturnToMenu;
        _gameControl.QuitRequested += () => Close();

        Closed += (_, _) => Cleanup();
    }

    private async void OnStartOffline()
    {
        _embeddedServer = new GameLoop(42);
        _embeddedServer.Start();

        var embeddedConnection = new EmbeddedServerConnection(_embeddedServer);
        _connection = embeddedConnection;
        _gameControl.SetConnection(_connection);
        await _connection.ConnectAsync("embedded://localhost");
        _gameControl.TransitionToPlaying();
    }

    private async void OnStartOnline()
    {
        var wsConnection = new WebSocketServerConnection();
        _connection = wsConnection;
        _gameControl.SetConnection(_connection);
        await _connection.ConnectAsync("ws://localhost:5000/ws");
        _gameControl.TransitionToPlaying();
    }

    private void OnReturnToMenu()
    {
        _gameControl.TransitionToMainMenu();
        CleanupConnection();
    }

    private void CleanupConnection()
    {
        _connection?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _connection = null;
        _embeddedServer?.Dispose();
        _embeddedServer = null;
    }

    private void Cleanup()
    {
        CleanupConnection();
    }
}
