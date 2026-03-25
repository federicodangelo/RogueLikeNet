using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Protocol.Messages;
using SkiaSharp;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Avalonia custom control that renders the game.
/// Manages screen state (main menu, gameplay, pause) and dispatches input/rendering accordingly.
/// Tile count adapts dynamically to window size — no scaling.
/// </summary>
public class GameRenderControl : Control
{
    private readonly TileRenderer _tileRenderer = new();
    private readonly ClientGameState _gameState = new();
    private IGameServerConnection? _connection;
    private DispatcherTimer? _renderTimer;
    private bool _initialized;

    private ScreenState _screenState = ScreenState.MainMenu;
    private int _menuIndex;
    private int _pauseIndex;
    private int _inventoryIndex;
    private string? _connectionError;

    public ClientGameState GameState => _gameState;
    public ScreenState CurrentScreen => _screenState;

    /// <summary>Fired when the player selects "Play Offline" from the main menu.</summary>
    public event Action? StartOfflineRequested;

    /// <summary>Fired when the player selects "Play Online" from the main menu.</summary>
    public event Action? StartOnlineRequested;

    /// <summary>Fired when the player selects "Return to Main Menu" from the pause menu.</summary>
    public event Action? ReturnToMenuRequested;

    /// <summary>Fired when the player selects "Quit" from the main menu.</summary>
    public event Action? QuitRequested;

    public void SetConnection(IGameServerConnection connection)
    {
        _connection = connection;
        _connection.OnWorldSnapshot += OnWorldSnapshot;
        _connection.OnWorldDelta += OnWorldDelta;
    }

    public void ClearConnection()
    {
        if (_connection != null)
        {
            _connection.OnWorldSnapshot -= OnWorldSnapshot;
            _connection.OnWorldDelta -= OnWorldDelta;
            _connection = null;
        }
    }

    public void TransitionToConnecting()
    {
        _connectionError = null;
        _screenState = ScreenState.Connecting;
        InvalidateVisual();
    }

    public void ShowConnectionError(string error)
    {
        _connectionError = error;
        _screenState = ScreenState.Connecting;
        InvalidateVisual();
    }

    public void TransitionToPlaying()
    {
        _screenState = ScreenState.Playing;
        InvalidateVisual();
    }

    public void TransitionToMainMenu()
    {
        ClearConnection();
        _screenState = ScreenState.MainMenu;
        _menuIndex = 0;
        _gameState.Clear();
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (!_initialized)
        {
            _tileRenderer.Initialize();
            _initialized = true;
        }

        Focusable = true;
        Focus();

        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _renderTimer.Tick += (_, _) => InvalidateVisual();
        _renderTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _renderTimer?.Stop();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        int totalCols = Math.Max(30, (int)(bounds.Width / TileRenderer.TileWidth));
        int totalRows = Math.Max(15, (int)(bounds.Height / TileRenderer.TileHeight));
        int pixelW = totalCols * TileRenderer.TileWidth;
        int pixelH = totalRows * TileRenderer.TileHeight;

        using var bitmap = new SKBitmap(pixelW, pixelH);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);

        switch (_screenState)
        {
            case ScreenState.MainMenu:
                _tileRenderer.RenderMainMenu(canvas, totalCols, totalRows, _menuIndex);
                break;
            case ScreenState.MainMenuHelp:
                _tileRenderer.RenderHelp(canvas, totalCols, totalRows);
                break;
            case ScreenState.Connecting:
                _tileRenderer.RenderConnecting(canvas, totalCols, totalRows, _connectionError);
                break;
            case ScreenState.Playing:
                _tileRenderer.RenderGame(canvas, _gameState, totalCols, totalRows);
                break;
            case ScreenState.Inventory:
                _tileRenderer.RenderGame(canvas, _gameState, totalCols, totalRows, true, _inventoryIndex);
                break;
            case ScreenState.Paused:
                _tileRenderer.RenderGame(canvas, _gameState, totalCols, totalRows);
                _tileRenderer.RenderPauseOverlay(canvas, totalCols, totalRows, _pauseIndex);
                break;
            case ScreenState.PausedHelp:
                _tileRenderer.RenderGame(canvas, _gameState, totalCols, totalRows);
                _tileRenderer.RenderHelp(canvas, totalCols, totalRows, isOverlay: true);
                break;
        }

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        var avaloniaBitmap = new Avalonia.Media.Imaging.Bitmap(stream);

        // Draw at actual pixel size — no scaling
        context.DrawImage(avaloniaBitmap, new Rect(0, 0, pixelW, pixelH));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (_screenState)
        {
            case ScreenState.MainMenu:
                HandleMainMenuInput(e);
                break;
            case ScreenState.MainMenuHelp:
                HandleHelpInput(e, ScreenState.MainMenu);
                break;
            case ScreenState.Connecting:
                HandleConnectingInput(e);
                break;
            case ScreenState.Playing:
                HandleGameInput(e);
                break;
            case ScreenState.Inventory:
                HandleInventoryInput(e);
                break;
            case ScreenState.Paused:
                HandlePauseInput(e);
                break;
            case ScreenState.PausedHelp:
                HandleHelpInput(e, ScreenState.Paused);
                break;
        }
    }

    // ── Main Menu Input ────────────────────────────────────────

    private void HandleMainMenuInput(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up or Key.W:
                _menuIndex = (_menuIndex + 3) % 4; // 4 items: Offline, Online, Help, Quit
                e.Handled = true;
                break;
            case Key.Down or Key.S:
                _menuIndex = (_menuIndex + 1) % 4;
                e.Handled = true;
                break;
            case Key.Enter or Key.Space:
                switch (_menuIndex)
                {
                    case 0: StartOfflineRequested?.Invoke(); break;
                    case 1: StartOnlineRequested?.Invoke(); break;
                    case 2: _screenState = ScreenState.MainMenuHelp; break;
                    case 3: QuitRequested?.Invoke(); break;
                }
                e.Handled = true;
                break;
        }
    }

    // ── Game Input ─────────────────────────────────────────────

    private void HandleGameInput(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _screenState = ScreenState.Paused;
            _pauseIndex = 0;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.I)
        {
            _screenState = ScreenState.Inventory;
            _inventoryIndex = 0;
            e.Handled = true;
            return;
        }

        var input = e.Key switch
        {
            Key.Up or Key.W => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 0, TargetY = -1 },
            Key.Down or Key.S => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 0, TargetY = 1 },
            Key.Left or Key.A => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = -1, TargetY = 0 },
            Key.Right or Key.D => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 },
            Key.Space => new ClientInputMsg { ActionType = ActionTypes.Wait },
            Key.F => new ClientInputMsg { ActionType = ActionTypes.Attack, TargetX = 0, TargetY = 0 },
            Key.G => new ClientInputMsg { ActionType = ActionTypes.PickUp },
            Key.D1 => new ClientInputMsg { ActionType = ActionTypes.UseItem, ItemSlot = 0 },
            Key.D2 => new ClientInputMsg { ActionType = ActionTypes.UseItem, ItemSlot = 1 },
            Key.D3 => new ClientInputMsg { ActionType = ActionTypes.UseItem, ItemSlot = 2 },
            Key.D4 => new ClientInputMsg { ActionType = ActionTypes.UseItem, ItemSlot = 3 },
            Key.Q => new ClientInputMsg { ActionType = ActionTypes.UseSkill, ItemSlot = 0, TargetX = 1, TargetY = 0 },
            Key.E => new ClientInputMsg { ActionType = ActionTypes.UseSkill, ItemSlot = 1, TargetX = 1, TargetY = 0 },
            Key.X => new ClientInputMsg { ActionType = ActionTypes.Drop, ItemSlot = 0 },
            _ => null
        };

        if (input != null && _connection != null)
        {
            input.Tick = _gameState.WorldTick;
            _ = _connection.SendInputAsync(input);
            e.Handled = true;
        }
    }

    // ── Pause Menu Input ───────────────────────────────────────

    private void HandlePauseInput(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                _screenState = ScreenState.Playing;
                e.Handled = true;
                break;
            case Key.Up or Key.W:
                _pauseIndex = (_pauseIndex + 2) % 3; // 3 items: Resume, Help, Return
                e.Handled = true;
                break;
            case Key.Down or Key.S:
                _pauseIndex = (_pauseIndex + 1) % 3;
                e.Handled = true;
                break;
            case Key.Enter or Key.Space:
                switch (_pauseIndex)
                {
                    case 0: _screenState = ScreenState.Playing; break;
                    case 1: _screenState = ScreenState.PausedHelp; break;
                    case 2: ReturnToMenuRequested?.Invoke(); break;
                }
                e.Handled = true;
                break;
        }
    }

    // ── Connecting Input ─────────────────────────────────────

    private void HandleConnectingInput(KeyEventArgs e)
    {
        if (_connectionError != null)
        {
            _screenState = ScreenState.MainMenu;
            _menuIndex = 0;
            _connectionError = null;
            e.Handled = true;
        }
    }

    // ── Inventory Input ─────────────────────────────────────

    private void HandleInventoryInput(KeyEventArgs e)
    {
        int cap = _gameState.PlayerHud?.InventoryCapacity ?? 4;
        if (cap < 1) cap = 4;

        switch (e.Key)
        {
            case Key.Escape:
                _screenState = ScreenState.Playing;
                e.Handled = true;
                break;
            case Key.Up or Key.W:
                _inventoryIndex = (_inventoryIndex + cap - 1) % cap;
                e.Handled = true;
                break;
            case Key.Down or Key.S:
                _inventoryIndex = (_inventoryIndex + 1) % cap;
                e.Handled = true;
                break;
            case Key.D1:
                SendInventoryAction(ActionTypes.UseItem, 0);
                e.Handled = true;
                break;
            case Key.D2:
                SendInventoryAction(ActionTypes.UseItem, 1);
                e.Handled = true;
                break;
            case Key.D3:
                SendInventoryAction(ActionTypes.UseItem, 2);
                e.Handled = true;
                break;
            case Key.D4:
                SendInventoryAction(ActionTypes.UseItem, 3);
                e.Handled = true;
                break;
            case Key.X:
                SendInventoryAction(ActionTypes.Drop, _inventoryIndex);
                e.Handled = true;
                break;
        }
    }

    private void SendInventoryAction(int actionType, int slot)
    {
        if (_connection == null) return;
        var input = new ClientInputMsg
        {
            ActionType = actionType,
            ItemSlot = slot,
            Tick = _gameState.WorldTick
        };
        _ = _connection.SendInputAsync(input);
    }

    // ── Help Input ─────────────────────────────────────────────

    private void HandleHelpInput(KeyEventArgs e, ScreenState returnTo)
    {
        if (e.Key is Key.Escape or Key.Enter)
        {
            _screenState = returnTo;
            e.Handled = true;
        }
    }

    // ── Server Messages ────────────────────────────────────────

    private void OnWorldSnapshot(WorldSnapshotMsg snapshot)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _gameState.ApplySnapshot(snapshot);
            InvalidateVisual();
        });
    }

    private void OnWorldDelta(WorldDeltaMsg delta)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _gameState.ApplyDelta(delta);
        });
    }
}
