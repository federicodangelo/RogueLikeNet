using Avalonia.Controls;
using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Client.Core.Rendering;

namespace RogueLikeNet.Client.Web;

/// <summary>
/// Main view for the Browser (WASM) client.
/// Runs a local game engine for offline play.
/// Can also connect to a remote server via WebSocket.
/// </summary>
public class MainView : UserControl
{
    private readonly GameRenderControl _gameControl;
    private IGameServerConnection? _connection;

    public MainView()
    {
        _gameControl = new GameRenderControl();
        Content = _gameControl;

        _gameControl.StartOfflineRequested += OnStartOffline;
        _gameControl.StartOnlineRequested += OnStartOnline;
        _gameControl.ReturnToMenuRequested += OnReturnToMenu;

        DetachedFromVisualTree += (_, _) => Cleanup();
    }

    private async void OnStartOffline()
    {
        _connection = new LocalGameConnection(42);
        _gameControl.SetConnection(_connection);
        await _connection.ConnectAsync("local://");
        _gameControl.TransitionToPlaying();
    }

    private async void OnStartOnline()
    {
        _gameControl.TransitionToConnecting();

        try
        {
            var wsConnection = new WebSocketServerConnection();
            _connection = wsConnection;
            _gameControl.SetConnection(_connection);
            await _connection.ConnectAsync("ws://localhost:5000/ws");
            _gameControl.TransitionToPlaying();
        }
        catch (Exception ex)
        {
            _gameControl.ClearConnection();
            _connection = null;
            _gameControl.ShowConnectionError(ex.Message);
        }
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
    }

    private void Cleanup()
    {
        CleanupConnection();
    }
}
