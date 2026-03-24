using Avalonia.Controls;
using Avalonia.Input;
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

        Opened += async (_, _) => await StartGame();
        Closed += (_, _) => Cleanup();
    }

    private async Task StartGame()
    {
        // For now, start an embedded server in standalone mode
        _embeddedServer = new GameLoop(42);
        _embeddedServer.Start();

        // Use embedded connection (in-process)
        var embeddedConnection = new EmbeddedServerConnection(_embeddedServer);
        _connection = embeddedConnection;
        _gameControl.SetConnection(_connection);
        await _connection.ConnectAsync("embedded://localhost");
    }

    private void Cleanup()
    {
        _connection?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _embeddedServer?.Dispose();
    }
}
