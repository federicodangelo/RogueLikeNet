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

        AttachedToVisualTree += async (_, _) => await StartGame();
        DetachedFromVisualTree += (_, _) => Cleanup();
    }

    private async Task StartGame()
    {
        // Use local game engine for offline WASM mode
        _connection = new LocalGameConnection(42);
        _gameControl.SetConnection(_connection);
        await _connection.ConnectAsync("local://");
    }

    private void Cleanup()
    {
        _connection?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
    }
}
