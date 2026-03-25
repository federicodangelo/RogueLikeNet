using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
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
    private GameDrawOperation? _drawOp;

    private ScreenState _screenState = ScreenState.MainMenu;
    private int _menuIndex;
    private int _pauseIndex;
    private int _inventoryIndex;
    private string? _connectionError;

    // Network message buffers — written from network thread, drained on UI thread during Render
    private readonly ConcurrentQueue<WorldDeltaMsg> _pendingDeltas = new();
    private volatile WorldSnapshotMsg? _pendingSnapshot;

    // Performance metrics
    private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
    private int _frameCount;
    private int _fps;
    private long _lastDeltaTicks;
    private int _latencyMs;

    // Chat
    private readonly List<string> _chatLog = new();
    private readonly ConcurrentQueue<ChatMsg> _pendingChats = new();
    private bool _chatInputActive;
    private string _chatInputText = "";

    // Screen shake
    private int _lastKnownHealth;
    private long _shakeUntilTicks;
    private readonly Random _shakeRng = new();

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
        _connection.OnChatReceived += OnChatReceived;
    }

    public void ClearConnection()
    {
        if (_connection != null)
        {
            _connection.OnWorldSnapshot -= OnWorldSnapshot;
            _connection.OnWorldDelta -= OnWorldDelta;
            _connection.OnChatReceived -= OnChatReceived;
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

        // Drain buffered network messages so the render always sees the latest state
        var snapshot = _pendingSnapshot;
        if (snapshot != null)
        {
            _pendingSnapshot = null;
            _gameState.ApplySnapshot(snapshot);
            // Discard any deltas older than this snapshot
            while (_pendingDeltas.TryDequeue(out _)) { }
        }
        while (_pendingDeltas.TryDequeue(out var delta))
            _gameState.ApplyDelta(delta);

        while (_pendingChats.TryDequeue(out var chat))
        {
            _chatLog.Add($"{chat.SenderName}: {chat.Text}");
            if (_chatLog.Count > 50) _chatLog.RemoveAt(0);
        }

        // Detect player damage → trigger screen shake
        int currentHealth = _gameState.PlayerHud?.Health ?? 0;
        if (_lastKnownHealth > 0 && currentHealth < _lastKnownHealth)
            _shakeUntilTicks = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 4; // 250ms
        _lastKnownHealth = currentHealth;

        // Update FPS counter
        _frameCount++;
        if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
        {
            _fps = _frameCount;
            _frameCount = 0;
            _fpsStopwatch.Restart();
        }

        _drawOp ??= new GameDrawOperation(this);
        _drawOp.Bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.Custom(_drawOp);
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
        if (_chatInputActive)
        {
            HandleChatInput(e);
            return;
        }

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

        if (e.Key == Key.T)
        {
            _chatInputActive = true;
            _chatInputText = "";
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
            case Key.Enter:
                SendInventoryAction(ActionTypes.UseItem, _inventoryIndex);
                e.Handled = true;
                break;
            case Key.E:
                SendInventoryAction(ActionTypes.Equip, _inventoryIndex);
                e.Handled = true;
                break;
            case Key.U:
                SendInventoryAction(ActionTypes.Unequip, 0); // 0 = weapon
                e.Handled = true;
                break;
            case Key.R:
                SendInventoryAction(ActionTypes.Unequip, 1); // 1 = armor
                e.Handled = true;
                break;
            case Key.X:
                SendInventoryAction(ActionTypes.Drop, _inventoryIndex);
                e.Handled = true;
                break;
        }
    }

    private void HandleChatInput(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                _chatInputActive = false;
                _chatInputText = "";
                e.Handled = true;
                break;
            case Key.Enter:
                if (_chatInputText.Length > 0 && _connection != null)
                    _ = _connection.SendChatAsync(_chatInputText);
                _chatInputActive = false;
                _chatInputText = "";
                e.Handled = true;
                break;
            case Key.Back:
                if (_chatInputText.Length > 0)
                    _chatInputText = _chatInputText[..^1];
                e.Handled = true;
                break;
            default:
                // Let OnTextInput handle character input
                break;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (_chatInputActive && !string.IsNullOrEmpty(e.Text))
        {
            if (_chatInputText.Length < 100)
                _chatInputText += e.Text;
            e.Handled = true;
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
        _pendingSnapshot = snapshot;
    }

    private void OnWorldDelta(WorldDeltaMsg delta)
    {
        long now = Stopwatch.GetTimestamp();
        if (_lastDeltaTicks > 0)
            _latencyMs = (int)((now - _lastDeltaTicks) * 1000 / Stopwatch.Frequency);
        _lastDeltaTicks = now;

        _pendingDeltas.Enqueue(delta);
    }

    private void OnChatReceived(ChatMsg msg)
    {
        _pendingChats.Enqueue(msg);
    }

    // ── GPU-accelerated Draw Operation ─────────────────────────

    private sealed class GameDrawOperation(GameRenderControl owner) : ICustomDrawOperation
    {
        public Rect Bounds { get; set; }
        public void Dispose() { }
        public bool Equals(ICustomDrawOperation? other) => false;
        public bool HitTest(Point p) => Bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature))
                as ISkiaSharpApiLeaseFeature;
            if (feature == null) return;

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;

            int totalCols = Math.Max(30, (int)(Bounds.Width / TileRenderer.TileWidth));
            int totalRows = Math.Max(15, (int)(Bounds.Height / TileRenderer.TileHeight));

            // Clip to our tile area so canvas.Clear() doesn't wipe other controls
            canvas.Save();
            canvas.ClipRect(SKRect.Create(0, 0,
                totalCols * TileRenderer.TileWidth, totalRows * TileRenderer.TileHeight));
            canvas.Clear(SKColors.Black);

            // Screen shake offset when player takes damage
            bool shaking = Stopwatch.GetTimestamp() < owner._shakeUntilTicks;
            if (shaking)
            {
                float sx = (owner._shakeRng.NextSingle() - 0.5f) * 8f;
                float sy = (owner._shakeRng.NextSingle() - 0.5f) * 8f;
                canvas.Translate(sx, sy);
            }

            var renderer = owner._tileRenderer;

            switch (owner._screenState)
            {
                case ScreenState.MainMenu:
                    renderer.RenderMainMenu(canvas, totalCols, totalRows, owner._menuIndex);
                    break;
                case ScreenState.MainMenuHelp:
                    renderer.RenderHelp(canvas, totalCols, totalRows);
                    break;
                case ScreenState.Connecting:
                    renderer.RenderConnecting(canvas, totalCols, totalRows, owner._connectionError);
                    break;
                case ScreenState.Playing:
                    renderer.RenderGame(canvas, owner._gameState, totalCols, totalRows);
                    break;
                case ScreenState.Inventory:
                    renderer.RenderGame(canvas, owner._gameState, totalCols, totalRows, true, owner._inventoryIndex);
                    break;
                case ScreenState.Paused:
                    renderer.RenderGame(canvas, owner._gameState, totalCols, totalRows);
                    renderer.RenderPauseOverlay(canvas, totalCols, totalRows, owner._pauseIndex);
                    break;
                case ScreenState.PausedHelp:
                    renderer.RenderGame(canvas, owner._gameState, totalCols, totalRows);
                    renderer.RenderHelp(canvas, totalCols, totalRows, isOverlay: true);
                    break;
            }

            if (owner._screenState is ScreenState.Playing or ScreenState.Inventory
                or ScreenState.Paused or ScreenState.PausedHelp)
            {
                renderer.RenderChatOverlay(canvas, totalCols, totalRows,
                    owner._chatLog, owner._chatInputActive, owner._chatInputText);
                renderer.RenderPerformanceOverlay(canvas, owner._fps, owner._latencyMs);
            }

            canvas.Restore();
        }
    }
}
