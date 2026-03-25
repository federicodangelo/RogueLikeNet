using System.Collections.Concurrent;
using System.Diagnostics;
using Engine.Platform;
using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core;

/// <summary>
/// Core game class — manages screen state, input dispatch, network message draining, and rendering.
/// Replaces the Avalonia GameRenderControl. Owned by platform-specific Program.cs files.
/// </summary>
public sealed class RogueLikeGame : GameBase
{
    private readonly TileRenderer _tileRenderer;
    private readonly ClientGameState _gameState = new();
    private readonly ParticleSystem _particles = new();
    private IGameServerConnection? _connection;

    private ScreenState _screenState = ScreenState.MainMenu;
    private int _menuIndex;
    private int _pauseIndex;
    private int _inventoryIndex;
    private string? _connectionError;

    // World seed — randomized each game start, editable in main menu
    private long _worldSeed = Random.Shared.NextInt64(0, 1_000_000_000);
    private bool _seedEditing;
    private string _seedEditText = "";

    // Network message buffers — written from network thread, drained each frame
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

    // Frame timing for particle update
    private long _lastFrameTicks;

    public ClientGameState GameState => _gameState;
    public ScreenState CurrentScreen => _screenState;

    /// <summary>Fired when the player selects "Play Offline" from the main menu.</summary>
    public event Action<long>? StartOfflineRequested;

    /// <summary>Fired when the player selects "Play Online" from the main menu.</summary>
    public event Action<long>? StartOnlineRequested;

    /// <summary>Fired when the player selects "Return to Main Menu" from the pause menu.</summary>
    public event Action? ReturnToMenuRequested;

    /// <summary>Fired when the player selects "Quit" from the main menu.</summary>
    public event Action? QuitRequested;

    public RogueLikeGame()
    {
        _tileRenderer = new TileRenderer();
    }

    /// <summary>
    /// Called once after Platform is set. Use for one-time init.
    /// Desktop: call before entering SDL loop.
    /// Web: call from InitializeLoop.
    /// </summary>
    public void Initialize(IPlatform platform)
    {
        Platform = platform;
    }

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
    }

    public void ShowConnectionError(string error)
    {
        _connectionError = error;
        _screenState = ScreenState.Connecting;
    }

    public void TransitionToPlaying()
    {
        _screenState = ScreenState.Playing;
    }

    public void TransitionToMainMenu()
    {
        ClearConnection();
        _screenState = ScreenState.MainMenu;
        _menuIndex = 0;
        _gameState.Clear();
    }

    /// <summary>
    /// Run one full frame: drain network, process input, render.
    /// Called from the platform game loop (SDL loop or requestAnimationFrame).
    /// </summary>
    public void RunFrame()
    {
        var input = Input;
        var renderer = SpriteRenderer;

        input.BeginFrame();
        input.ProcessEvents();

        // Check quit
        if (input.QuitRequested)
        {
            QuitRequested?.Invoke();
            input.EndFrame();
            return;
        }

        // Drain buffered network messages
        DrainNetworkMessages();

        // Detect player damage → trigger screen shake
        int currentHealth = _gameState.PlayerHud?.Health ?? 0;
        if (_lastKnownHealth > 0 && currentHealth < _lastKnownHealth)
            _shakeUntilTicks = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 4; // 250ms
        _lastKnownHealth = currentHealth;

        // Update particles with frame delta time
        long nowTicks = Stopwatch.GetTimestamp();
        float dt = _lastFrameTicks > 0
            ? (float)(nowTicks - _lastFrameTicks) / Stopwatch.Frequency
            : 1f / 60f;
        _lastFrameTicks = nowTicks;
        dt = Math.Clamp(dt, 0.001f, 0.1f); // guard against huge spikes
        _particles.Update(dt);

        // Update FPS counter
        _frameCount++;
        if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
        {
            _fps = _frameCount;
            _frameCount = 0;
            _fpsStopwatch.Restart();
        }

        // Handle input based on current screen
        HandleInput(input);

        // Render
        renderer.Update();
        renderer.BeginFrame();

        int windowW = renderer.WindowWidth;
        int windowH = renderer.WindowHeight;
        int totalCols = Math.Max(30, windowW / TileRenderer.TileWidth);
        int totalRows = Math.Max(15, windowH / TileRenderer.TileHeight);

        // Screen shake offset
        float shakeX = 0, shakeY = 0;
        if (Stopwatch.GetTimestamp() < _shakeUntilTicks)
        {
            shakeX = (_shakeRng.NextSingle() - 0.5f) * 8f;
            shakeY = (_shakeRng.NextSingle() - 0.5f) * 8f;
        }

        switch (_screenState)
        {
            case ScreenState.MainMenu:
                _tileRenderer.RenderMainMenu(renderer, totalCols, totalRows, _menuIndex, _worldSeed, _seedEditing, _seedEditText);
                break;
            case ScreenState.MainMenuHelp:
                _tileRenderer.RenderHelp(renderer, totalCols, totalRows);
                break;
            case ScreenState.Connecting:
                _tileRenderer.RenderConnecting(renderer, totalCols, totalRows, _connectionError);
                break;
            case ScreenState.Playing:
                _tileRenderer.RenderGame(renderer, _gameState, totalCols, totalRows, shakeX, shakeY);
                break;
            case ScreenState.Inventory:
                _tileRenderer.RenderGame(renderer, _gameState, totalCols, totalRows, shakeX, shakeY,
                    inventoryMode: true, inventoryIndex: _inventoryIndex);
                break;
            case ScreenState.Paused:
                _tileRenderer.RenderGame(renderer, _gameState, totalCols, totalRows, shakeX, shakeY);
                _tileRenderer.RenderPauseOverlay(renderer, totalCols, totalRows, _pauseIndex);
                break;
            case ScreenState.PausedHelp:
                _tileRenderer.RenderGame(renderer, _gameState, totalCols, totalRows, shakeX, shakeY);
                _tileRenderer.RenderHelp(renderer, totalCols, totalRows, isOverlay: true);
                break;
        }

        if (_screenState is ScreenState.Playing or ScreenState.Inventory
            or ScreenState.Paused or ScreenState.PausedHelp)
        {
            // Render particles and minimap over the game world
            int gameCols = totalCols - TileRenderer.HudColumns;
            int halfW = gameCols / 2;
            int halfH = totalRows / 2;
            _particles.Render(renderer, _gameState.PlayerX, _gameState.PlayerY,
                halfW, halfH, shakeX, shakeY);
            _tileRenderer.RenderMinimap(renderer, _gameState, gameCols, totalRows);

            _tileRenderer.RenderChatOverlay(renderer, totalCols, totalRows,
                _chatLog, _chatInputActive, _chatInputText);
            _tileRenderer.RenderPerformanceOverlay(renderer, _fps, _latencyMs);
        }

        renderer.EndFrame();
        input.EndFrame();
    }

    private void DrainNetworkMessages()
    {
        var snapshot = _pendingSnapshot;
        if (snapshot != null)
        {
            _pendingSnapshot = null;
            _gameState.ApplySnapshot(snapshot);
            while (_pendingDeltas.TryDequeue(out _)) { }
        }
        while (_pendingDeltas.TryDequeue(out var delta))
            _gameState.ApplyDelta(delta);

        // Feed combat events to particle system
        foreach (var evt in _gameState.PendingCombatEvents)
        {
            _particles.SpawnDamageNumber(evt.TargetX, evt.TargetY, evt.Damage, evt.TargetDied);
            _particles.SpawnHitSparks(evt.AttackerX, evt.AttackerY, evt.TargetX, evt.TargetY, evt.TargetDied);
        }
        _gameState.DrainCombatEvents();

        while (_pendingChats.TryDequeue(out var chat))
        {
            _chatLog.Add($"{chat.SenderName}: {chat.Text}");
            if (_chatLog.Count > 50) _chatLog.RemoveAt(0);
        }
    }

    // ── Input Dispatch ────────────────────────────────────────

    private void HandleInput(IInputManager input)
    {
        switch (_screenState)
        {
            case ScreenState.MainMenu:
                HandleMainMenuInput(input);
                break;
            case ScreenState.MainMenuHelp:
                HandleHelpInput(input, ScreenState.MainMenu);
                break;
            case ScreenState.Connecting:
                HandleConnectingInput(input);
                break;
            case ScreenState.Playing:
                HandleGameInput(input);
                break;
            case ScreenState.Inventory:
                HandleInventoryInput(input);
                break;
            case ScreenState.Paused:
                HandlePauseInput(input);
                break;
            case ScreenState.PausedHelp:
                HandleHelpInput(input, ScreenState.Paused);
                break;
        }
    }

    private void HandleMainMenuInput(IInputManager input)
    {
        // Seed editing mode — captures text input
        if (_seedEditing)
        {
            HandleSeedEditing(input);
            return;
        }

        int itemCount = 6; // Offline, Online, Seed, Randomize, Help, Quit
        if (input.IsActionPressed(InputAction.MenuUp))
            _menuIndex = (_menuIndex + itemCount - 1) % itemCount;
        else if (input.IsActionPressed(InputAction.MenuDown))
            _menuIndex = (_menuIndex + 1) % itemCount;
        else if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            switch (_menuIndex)
            {
                case 0: StartOfflineRequested?.Invoke(_worldSeed); break;
                case 1: StartOnlineRequested?.Invoke(_worldSeed); break;
                case 2:
                    _seedEditing = true;
                    _seedEditText = _worldSeed.ToString();
                    break;
                case 3: _worldSeed = Random.Shared.NextInt64(0, 1_000_000_000); break;
                case 4: _screenState = ScreenState.MainMenuHelp; break;
                case 5: QuitRequested?.Invoke(); break;
            }
        }
    }

    private void HandleSeedEditing(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _seedEditing = false;
            return;
        }

        for (int i = 0; i < input.TextInputBackspacesCount; i++)
        {
            if (_seedEditText.Length > 0)
                _seedEditText = _seedEditText[..^1];
        }

        if (input.TextInputReturnsCount > 0)
        {
            if (long.TryParse(_seedEditText, out long parsed))
                _worldSeed = parsed;
            _seedEditing = false;
            return;
        }

        string typed = input.TextInput;
        foreach (char c in typed)
        {
            if (char.IsAsciiDigit(c) && _seedEditText.Length < 18)
                _seedEditText += c;
        }
    }

    private void HandleGameInput(IInputManager input)
    {
        if (_chatInputActive)
        {
            HandleChatInput(input);
            return;
        }

        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _screenState = ScreenState.Paused;
            _pauseIndex = 0;
            return;
        }

        if (input.IsActionPressed(InputAction.OpenInventory))
        {
            _screenState = ScreenState.Inventory;
            _inventoryIndex = 0;
            return;
        }

        if (input.IsActionPressed(InputAction.OpenChat))
        {
            _chatInputActive = true;
            _chatInputText = "";
            return;
        }

        ClientInputMsg? msg = null;

        if (input.IsActionPressed(InputAction.MoveUp))
            msg = new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 0, TargetY = -1 };
        else if (input.IsActionPressed(InputAction.MoveDown))
            msg = new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 0, TargetY = 1 };
        else if (input.IsActionPressed(InputAction.MoveLeft))
            msg = new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = -1, TargetY = 0 };
        else if (input.IsActionPressed(InputAction.MoveRight))
            msg = new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 };
        else if (input.IsActionPressed(InputAction.Wait))
            msg = new ClientInputMsg { ActionType = ActionTypes.Wait };
        else if (input.IsActionPressed(InputAction.Attack))
            msg = new ClientInputMsg { ActionType = ActionTypes.Attack, TargetX = 0, TargetY = 0 };
        else if (input.IsActionPressed(InputAction.PickUp))
            msg = new ClientInputMsg { ActionType = ActionTypes.PickUp };
        else if (input.IsActionPressed(InputAction.UseItem1))
            msg = new ClientInputMsg { ActionType = ActionTypes.UseItem, ItemSlot = 0 };
        else if (input.IsActionPressed(InputAction.UseItem2))
            msg = new ClientInputMsg { ActionType = ActionTypes.UseItem, ItemSlot = 1 };
        else if (input.IsActionPressed(InputAction.UseItem3))
            msg = new ClientInputMsg { ActionType = ActionTypes.UseItem, ItemSlot = 2 };
        else if (input.IsActionPressed(InputAction.UseItem4))
            msg = new ClientInputMsg { ActionType = ActionTypes.UseItem, ItemSlot = 3 };
        else if (input.IsActionPressed(InputAction.UseSkill1))
            msg = new ClientInputMsg { ActionType = ActionTypes.UseSkill, ItemSlot = 0, TargetX = 1, TargetY = 0 };
        else if (input.IsActionPressed(InputAction.UseSkill2))
            msg = new ClientInputMsg { ActionType = ActionTypes.UseSkill, ItemSlot = 1, TargetX = 1, TargetY = 0 };
        else if (input.IsActionPressed(InputAction.Drop))
            msg = new ClientInputMsg { ActionType = ActionTypes.Drop, ItemSlot = 0 };

        if (msg != null && _connection != null)
        {
            msg.Tick = _gameState.WorldTick;
            _ = _connection.SendInputAsync(msg);
        }
    }

    private void HandlePauseInput(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _screenState = ScreenState.Playing;
            return;
        }

        if (input.IsActionPressed(InputAction.MenuUp))
            _pauseIndex = (_pauseIndex + 2) % 3;
        else if (input.IsActionPressed(InputAction.MenuDown))
            _pauseIndex = (_pauseIndex + 1) % 3;
        else if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            switch (_pauseIndex)
            {
                case 0: _screenState = ScreenState.Playing; break;
                case 1: _screenState = ScreenState.PausedHelp; break;
                case 2: ReturnToMenuRequested?.Invoke(); break;
            }
        }
    }

    private void HandleConnectingInput(IInputManager input)
    {
        if (_connectionError != null && input.IsActionPressed(InputAction.MenuConfirm))
        {
            _screenState = ScreenState.MainMenu;
            _menuIndex = 0;
            _connectionError = null;
        }
    }

    private void HandleInventoryInput(IInputManager input)
    {
        int cap = _gameState.PlayerHud?.InventoryCapacity ?? 4;
        if (cap < 1) cap = 4;

        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _screenState = ScreenState.Playing;
            return;
        }

        if (input.IsActionPressed(InputAction.MenuUp))
            _inventoryIndex = (_inventoryIndex + cap - 1) % cap;
        else if (input.IsActionPressed(InputAction.MenuDown))
            _inventoryIndex = (_inventoryIndex + 1) % cap;
        else if (input.IsActionPressed(InputAction.UseItem1))
            SendInventoryAction(ActionTypes.UseItem, 0);
        else if (input.IsActionPressed(InputAction.UseItem2))
            SendInventoryAction(ActionTypes.UseItem, 1);
        else if (input.IsActionPressed(InputAction.UseItem3))
            SendInventoryAction(ActionTypes.UseItem, 2);
        else if (input.IsActionPressed(InputAction.UseItem4))
            SendInventoryAction(ActionTypes.UseItem, 3);
        else if (input.IsActionPressed(InputAction.MenuConfirm))
            SendInventoryAction(ActionTypes.UseItem, _inventoryIndex);
        else if (input.IsActionPressed(InputAction.Equip))
            SendInventoryAction(ActionTypes.Equip, _inventoryIndex);
        else if (input.IsActionPressed(InputAction.UnequipSlot1))
            SendInventoryAction(ActionTypes.Unequip, 0);
        else if (input.IsActionPressed(InputAction.UnequipSlot2))
            SendInventoryAction(ActionTypes.Unequip, 1);
        else if (input.IsActionPressed(InputAction.Drop))
            SendInventoryAction(ActionTypes.Drop, _inventoryIndex);
    }

    private void HandleChatInput(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _chatInputActive = false;
            _chatInputText = "";
            return;
        }

        // Handle backspaces
        for (int i = 0; i < input.TextInputBackspacesCount; i++)
        {
            if (_chatInputText.Length > 0)
                _chatInputText = _chatInputText[..^1];
        }

        // Handle enter/return
        if (input.TextInputReturnsCount > 0)
        {
            if (_chatInputText.Length > 0 && _connection != null)
                _ = _connection.SendChatAsync(_chatInputText);
            _chatInputActive = false;
            _chatInputText = "";
            return;
        }

        // Append typed characters
        string typed = input.TextInput;
        if (typed.Length > 0 && _chatInputText.Length < 100)
        {
            _chatInputText += typed;
            if (_chatInputText.Length > 100)
                _chatInputText = _chatInputText[..100];
        }
    }

    private void SendInventoryAction(int actionType, int slot)
    {
        if (_connection == null) return;
        var msg = new ClientInputMsg
        {
            ActionType = actionType,
            ItemSlot = slot,
            Tick = _gameState.WorldTick
        };
        _ = _connection.SendInputAsync(msg);
    }

    private void HandleHelpInput(IInputManager input, ScreenState returnTo)
    {
        if (input.IsActionPressed(InputAction.MenuBack) || input.IsActionPressed(InputAction.MenuConfirm))
            _screenState = returnTo;
    }

    // ── Server Messages ────────────────────────────────────────

    private void OnWorldSnapshot(WorldSnapshotMsg snapshot) => _pendingSnapshot = snapshot;
    private void OnWorldDelta(WorldDeltaMsg delta)
    {
        long now = Stopwatch.GetTimestamp();
        if (_lastDeltaTicks > 0)
            _latencyMs = (int)((now - _lastDeltaTicks) * 1000 / Stopwatch.Frequency);
        _lastDeltaTicks = now;
        _pendingDeltas.Enqueue(delta);
    }
    private void OnChatReceived(ChatMsg msg) => _pendingChats.Enqueue(msg);

    public override void Dispose()
    {
        ClearConnection();
        Platform?.Dispose();
        GC.SuppressFinalize(this);
    }
}
