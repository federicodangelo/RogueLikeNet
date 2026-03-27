using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Engine.Platform;
using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
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
    private volatile bool _connectionFirstDeltaProcessed;

    private ScreenState _screenState = ScreenState.MainMenu;
    private int _menuIndex;
    private int _pauseIndex;
    private string? _connectionError;

    // Class selection state
    private int _selectedClassIndex;
    private string _playerName = "Hero";
    private bool _nameEditing;
    private string _nameEditText = "";
    private bool _classSelectIsOnline; // remembers which mode was chosen

    // HUD layout system
    private readonly HudLayout _hudLayout;
    private readonly HudLayout _inventoryLayout;

    // World seed — randomized each game start, editable in main menu
    private long _worldSeed = Random.Shared.NextInt64(0, 1_000_000_000);
    private bool _seedEditing;
    private string _seedEditText = "";

    // Network message buffers — written from network thread, drained each frame
    private readonly ConcurrentQueue<WorldDeltaMsg> _pendingDeltas = new();

    // Performance metrics
    private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
    private int _frameCount;
    private int _fps;
    private long _lastDeltaTicks;
    private int _latencyMs;
    private long _lastBytesSent;
    private long _lastBytesReceived;
    private double _bwInKBps;
    private double _bwOutKBps;

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

    // Movement hold-to-repeat: fire immediately on press, repeat only after 250ms initial delay
    private static readonly long MoveRepeatDelayTicks = Stopwatch.Frequency / 4; // 250 ms
    private InputAction? _heldMoveAction;
    private long _moveHeldSinceTicks;

    public ClientGameState GameState => _gameState;
    public ScreenState CurrentScreen => _screenState;

    /// <summary>Fired when the player selects "Play Offline" from the main menu.</summary>
    public event Action<long, int, string>? StartOfflineRequested;

    /// <summary>Fired when the player selects "Play Online" from the main menu.</summary>
    public event Action<int, string>? StartOnlineRequested;

    /// <summary>Fired when the player selects "Return to Main Menu" from the pause menu.</summary>
    public event Action? ReturnToMenuRequested;

    /// <summary>Fired when the player selects "Quit" from the main menu.</summary>
    public event Action? QuitRequested;

    public bool IsFirstDeltaProcessed => _connectionFirstDeltaProcessed;

    public RogueLikeGame()
    {
        _tileRenderer = new TileRenderer();
        _hudLayout = CreateHudLayout();
        _inventoryLayout = CreateInventoryLayout();
    }

    private static HudLayout CreateHudLayout()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "HP", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 4 });
        layout.AddSection(new HudSection { Name = "Stats", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 4 });
        layout.AddSection(new HudSection { Name = "Skills", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 5 });
        layout.AddSection(new HudSection { Name = "Equipment", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 5 });
        layout.AddSection(new HudSection { Name = "QuickSlots", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 8, AcceptsInput = true });
        layout.AddSection(new HudSection { Name = "FloorItems", Anchor = HudAnchor.Top, IsFixedHeight = false, Scrollable = true });
        layout.AddSection(new HudSection { Name = "Controls", Anchor = HudAnchor.Bottom, IsFixedHeight = true, FixedHeight = 2 });
        return layout;
    }

    private static HudLayout CreateInventoryLayout()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "InvHeader", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 4 });
        layout.AddSection(new HudSection { Name = "InvItems", Anchor = HudAnchor.Top, IsFixedHeight = false, Scrollable = true, AcceptsInput = true, UseScrollIndicators = true });
        layout.AddSection(new HudSection { Name = "InvEquipment", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        layout.AddSection(new HudSection { Name = "InvActions", Anchor = HudAnchor.Bottom, IsFixedHeight = true, FixedHeight = 9 });
        // Set initial focus on the items section
        layout.SetFocus(1);
        return layout;
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
        _connectionFirstDeltaProcessed = false;
        _connection.OnWorldDelta += OnNetworkWorldDelta;
        _connection.OnChatReceived += OnNetworkChatReceived;
    }

    public void ClearConnection()
    {
        if (_connection != null)
        {
            _connection.OnWorldDelta -= OnNetworkWorldDelta;
            _connection.OnChatReceived -= OnNetworkChatReceived;
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
        int currentHealth = _gameState.PlayerState?.Health ?? 0;
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

            // Update bandwidth stats
            if (_connection != null)
            {
                long curSent = _connection.BytesSent;
                long curRecv = _connection.BytesReceived;
                double elapsed = _fpsStopwatch.Elapsed.TotalSeconds;
                _bwOutKBps = (curSent - _lastBytesSent) / 1024.0 / elapsed;
                _bwInKBps = (curRecv - _lastBytesReceived) / 1024.0 / elapsed;
                _lastBytesSent = curSent;
                _lastBytesReceived = curRecv;
            }
            else
            {
                _bwInKBps = 0;
                _bwOutKBps = 0;
            }

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

        void RenderGameScreen(bool inventoryMode = false)
        {
            _tileRenderer.RenderGame(
                renderer, _gameState, totalCols, totalRows, shakeX, shakeY,
                layout: inventoryMode ? _inventoryLayout : _hudLayout,
                inventoryMode: inventoryMode
            );

            // Render particles
            int gameCols = totalCols - TileRenderer.HudColumns;
            int halfW = gameCols / 2;
            int halfH = totalRows / 2;
            _particles.Render(renderer, _gameState.PlayerX, _gameState.PlayerY,
                halfW, halfH, shakeX, shakeY);

            // Disable minimap until further notice (its not usually used in roguelikes)
            //_tileRenderer.RenderMinimap(renderer, _gameState, gameCols, totalRows);

            _tileRenderer.RenderChatOverlay(renderer, totalCols, totalRows,
                _chatLog, _chatInputActive, _chatInputText);
            _tileRenderer.RenderPerformanceOverlay(renderer, _fps, _latencyMs,
                _bwInKBps, _bwOutKBps);
        }

        switch (_screenState)
        {
            case ScreenState.MainMenu:
                _tileRenderer.RenderMainMenu(renderer, totalCols, totalRows, _menuIndex, _worldSeed, _seedEditing, _seedEditText);
                break;
            case ScreenState.MainMenuHelp:
                _tileRenderer.RenderHelp(renderer, totalCols, totalRows);
                break;
            case ScreenState.ClassSelect:
                _tileRenderer.RenderClassSelect(renderer, totalCols, totalRows, _selectedClassIndex, _playerName, _nameEditing, _nameEditText);
                break;
            case ScreenState.Connecting:
                _tileRenderer.RenderConnecting(renderer, totalCols, totalRows, _connectionError);
                break;
            case ScreenState.Playing:
                RenderGameScreen();
                break;
            case ScreenState.Inventory:
                RenderGameScreen(inventoryMode: true);
                break;
            case ScreenState.Paused:
                RenderGameScreen();
                _tileRenderer.RenderPauseOverlay(renderer, totalCols, totalRows, _pauseIndex);
                break;
            case ScreenState.PausedHelp:
                RenderGameScreen();
                _tileRenderer.RenderHelp(renderer, totalCols, totalRows, isOverlay: true);
                break;
        }

        renderer.EndFrame();
        input.EndFrame();
    }

    private void DrainNetworkMessages()
    {
        while (_pendingDeltas.TryDequeue(out var delta))
        {
            _connectionFirstDeltaProcessed = true;
            _gameState.ApplyDelta(delta);
        }

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
            case ScreenState.ClassSelect:
                HandleClassSelectInput(input);
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
                case 0:
                    _classSelectIsOnline = false;
                    _screenState = ScreenState.ClassSelect;
                    _selectedClassIndex = 0;
                    break;
                case 1:
                    _classSelectIsOnline = true;
                    _screenState = ScreenState.ClassSelect;
                    _selectedClassIndex = 0;
                    break;
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

    private void HandleClassSelectInput(IInputManager input)
    {
        if (_nameEditing)
        {
            HandleNameEditing(input);
            return;
        }

        int classCount = ClassDefinitions.All.Length;

        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _screenState = ScreenState.MainMenu;
            return;
        }

        if (input.IsActionPressed(InputAction.MoveLeft) || input.IsActionPressed(InputAction.MenuUp))
            _selectedClassIndex = (_selectedClassIndex + classCount - 1) % classCount;
        else if (input.IsActionPressed(InputAction.MoveRight) || input.IsActionPressed(InputAction.MenuDown))
            _selectedClassIndex = (_selectedClassIndex + 1) % classCount;
        else if (input.IsActionPressed(InputAction.OpenChat)) // 'T' to edit name
        {
            _nameEditing = true;
            _nameEditText = _playerName;
        }
        else if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            int classId = ClassDefinitions.All[_selectedClassIndex].ClassId;
            if (_classSelectIsOnline)
                StartOnlineRequested?.Invoke(classId, _playerName);
            else
                StartOfflineRequested?.Invoke(_worldSeed, classId, _playerName);
        }
    }

    private void HandleNameEditing(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _nameEditing = false;
            return;
        }

        for (int i = 0; i < input.TextInputBackspacesCount; i++)
        {
            if (_nameEditText.Length > 0)
                _nameEditText = _nameEditText[..^1];
        }

        if (input.TextInputReturnsCount > 0)
        {
            if (_nameEditText.Length > 0)
                _playerName = _nameEditText;
            _nameEditing = false;
            return;
        }

        string typed = input.TextInput;
        foreach (char c in typed)
        {
            if ((char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-') && _nameEditText.Length < 16)
                _nameEditText += c;
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
            // Reset inventory layout: focus on items section, scroll to top
            _inventoryLayout.SetFocus(1); // InvItems section
            foreach (var s in _inventoryLayout.Sections)
            {
                s.SelectedIndex = 0;
                s.ScrollOffset = 0;
            }
            return;
        }

        if (input.IsActionPressed(InputAction.OpenChat))
        {
            _chatInputActive = true;
            _chatInputText = "";
            return;
        }

        ClientInputMsg? msg = null;

        // Determine held movement direction, applying a 250ms initial-repeat delay
        InputAction? activeMoveAction = null;
        if (input.IsActionDown(InputAction.MoveUp)) activeMoveAction = InputAction.MoveUp;
        else if (input.IsActionDown(InputAction.MoveDown)) activeMoveAction = InputAction.MoveDown;
        else if (input.IsActionDown(InputAction.MoveLeft)) activeMoveAction = InputAction.MoveLeft;
        else if (input.IsActionDown(InputAction.MoveRight)) activeMoveAction = InputAction.MoveRight;

        if (activeMoveAction != null)
        {
            long now = Stopwatch.GetTimestamp();
            if (_heldMoveAction != activeMoveAction)
            {
                // New direction — treat as a fresh press and restart the hold timer
                _heldMoveAction = activeMoveAction;
                _moveHeldSinceTicks = now;
            }

            bool fireMove = input.IsActionPressed(activeMoveAction.Value)
                            || now - _moveHeldSinceTicks >= MoveRepeatDelayTicks;
            if (fireMove)
            {
                msg = activeMoveAction.Value switch
                {
                    InputAction.MoveUp => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 0, TargetY = -1 },
                    InputAction.MoveDown => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 0, TargetY = 1 },
                    InputAction.MoveLeft => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = -1, TargetY = 0 },
                    InputAction.MoveRight => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 },
                    _ => null
                };
            }
        }
        else
        {
            _heldMoveAction = null;
        }

        if (msg == null)
        {
            if (input.IsActionPressed(InputAction.Wait))
                msg = new ClientInputMsg { ActionType = ActionTypes.Wait };
            else if (input.IsActionPressed(InputAction.Attack))
                msg = new ClientInputMsg { ActionType = ActionTypes.Attack, TargetX = 0, TargetY = 0 };
            else if (input.IsActionPressed(InputAction.PickUp))
                msg = new ClientInputMsg { ActionType = ActionTypes.PickUp };
            else if (input.IsActionPressed(InputAction.UseItem1))
                msg = new ClientInputMsg { ActionType = ActionTypes.UseQuickSlot, ItemSlot = 0 };
            else if (input.IsActionPressed(InputAction.UseItem2))
                msg = new ClientInputMsg { ActionType = ActionTypes.UseQuickSlot, ItemSlot = 1 };
            else if (input.IsActionPressed(InputAction.UseItem3))
                msg = new ClientInputMsg { ActionType = ActionTypes.UseQuickSlot, ItemSlot = 2 };
            else if (input.IsActionPressed(InputAction.UseItem4))
                msg = new ClientInputMsg { ActionType = ActionTypes.UseQuickSlot, ItemSlot = 3 };
            else if (input.IsActionPressed(InputAction.UseSkill1))
                msg = new ClientInputMsg { ActionType = ActionTypes.UseSkill, ItemSlot = 0, TargetX = 1, TargetY = 0 };
            else if (input.IsActionPressed(InputAction.UseSkill2))
                msg = new ClientInputMsg { ActionType = ActionTypes.UseSkill, ItemSlot = 1, TargetX = 1, TargetY = 0 };
        }

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
        int cap = _gameState.PlayerState?.InventoryCapacity ?? 4;
        if (cap < 1) cap = 4;

        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _screenState = ScreenState.Playing;
            return;
        }

        if (input.IsActionPressed(InputAction.CycleSection))
        {
            _inventoryLayout.CycleFocus();
            return;
        }

        var focused = _inventoryLayout.FocusedSection;
        if (focused == null) return;

        switch (focused.Name)
        {
            case "InvItems":
                HandleInvItemsInput(input, focused, cap);
                break;
            case "InvEquipment":
                HandleInvEquipmentInput(input, focused, cap);
                break;
        }
    }

    private void HandleInvItemsInput(IInputManager input, HudSection section, int cap)
    {
        if (input.IsActionPressed(InputAction.MenuUp))
        {
            if (section.SelectedIndex > 0)
                section.ScrollUp();
            else
            {
                // Cross to previous AcceptsInput section at its last item
                var prev = _inventoryLayout.FocusPreviousInputSection();
                if (prev != null && prev != section)
                {
                    int prevMax = GetInvSectionItemCount(prev, cap);
                    prev.SelectedIndex = Math.Max(0, prevMax - 1);
                    prev.EnsureSelectionVisible();
                }
                else
                {
                    // Single section — loop to last item
                    section.SelectedIndex = Math.Max(0, cap - 1);
                    section.EnsureSelectionVisible();
                }
            }
        }
        else if (input.IsActionPressed(InputAction.MenuDown))
        {
            if (section.SelectedIndex < cap - 1)
                section.ScrollDown(cap);
            else
            {
                // Cross to next AcceptsInput section at its first item
                var next = _inventoryLayout.FocusNextInputSection();
                if (next != null && next != section)
                {
                    next.SelectedIndex = 0;
                    next.ScrollOffset = 0;
                }
                else
                {
                    // Single section — loop to first item
                    section.SelectedIndex = 0;
                    section.ScrollOffset = 0;
                }
            }
        }
        else if (input.IsActionPressed(InputAction.UseItem1))
            SendInventoryAction(ActionTypes.SetQuickSlot, 0, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.UseItem2))
            SendInventoryAction(ActionTypes.SetQuickSlot, 1, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.UseItem3))
            SendInventoryAction(ActionTypes.SetQuickSlot, 2, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.UseItem4))
            SendInventoryAction(ActionTypes.SetQuickSlot, 3, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.MenuConfirm))
            SendInventoryAction(ActionTypes.UseItem, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.Equip) && section.SelectedIndex < cap)
            SendInventoryAction(ActionTypes.Equip, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.Drop) && section.SelectedIndex < cap)
            SendInventoryAction(ActionTypes.Drop, section.SelectedIndex);
    }

    private void HandleInvEquipmentInput(IInputManager input, HudSection section, int cap)
    {
        const int EquipmentSlots = 2;
        if (input.IsActionPressed(InputAction.MenuUp))
        {
            if (section.SelectedIndex > 0)
                section.SelectedIndex--;
            else
            {
                // Cross to previous AcceptsInput section at its last item
                var prev = _inventoryLayout.FocusPreviousInputSection();
                if (prev != null && prev != section)
                {
                    int prevMax = GetInvSectionItemCount(prev, cap);
                    prev.SelectedIndex = Math.Max(0, prevMax - 1);
                    prev.EnsureSelectionVisible();
                }
                else
                {
                    // Single section — loop to last equipment slot
                    section.SelectedIndex = EquipmentSlots - 1;
                }
            }
        }
        else if (input.IsActionPressed(InputAction.MenuDown))
        {
            if (section.SelectedIndex < EquipmentSlots - 1)
                section.SelectedIndex++;
            else
            {
                // Cross to next AcceptsInput section at its first item
                var next = _inventoryLayout.FocusNextInputSection();
                if (next != null && next != section)
                {
                    next.SelectedIndex = 0;
                    next.ScrollOffset = 0;
                }
                else
                {
                    // Single section — loop to first slot
                    section.SelectedIndex = 0;
                }
            }
        }
        else if (input.IsActionPressed(InputAction.MenuConfirm) || input.IsActionPressed(InputAction.Equip))
        {
            // Unequip: 0 = weapon, 1 = armor
            SendInventoryAction(ActionTypes.Unequip, section.SelectedIndex);
        }
    }

    /// <summary>Returns the total number of selectable items in an inventory section.</summary>
    private static int GetInvSectionItemCount(HudSection section, int cap) => section.Name switch
    {
        "InvItems" => cap,
        "InvEquipment" => 2,
        _ => 0
    };

    private void SendInventoryAction(int actionType, int slot, int targetSlot = 0)
    {
        if (_connection == null) return;
        var msg = new ClientInputMsg
        {
            ActionType = actionType,
            ItemSlot = slot,
            TargetSlot = targetSlot,
            Tick = _gameState.WorldTick
        };
        _ = _connection.SendInputAsync(msg);
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

    private void HandleHelpInput(IInputManager input, ScreenState returnTo)
    {
        if (input.IsActionPressed(InputAction.MenuBack) || input.IsActionPressed(InputAction.MenuConfirm))
            _screenState = returnTo;
    }

    // ── Server Messages ────────────────────────────────────────

    private void OnNetworkWorldDelta(WorldDeltaMsg delta)
    {
        // Called from network thread, so just enqueue the delta for processing on the main thread. 
        // Also track latency based on when deltas are received.
        long now = Stopwatch.GetTimestamp();
        if (_lastDeltaTicks > 0)
            _latencyMs = (int)((now - _lastDeltaTicks) * 1000 / Stopwatch.Frequency);
        _lastDeltaTicks = now;
        _pendingDeltas.Enqueue(delta);
    }

    private void OnNetworkChatReceived(ChatMsg msg)
    {
        // Called from network thread, so just enqueue for processing on the main thread.
        _pendingChats.Enqueue(msg);
    }

    public override void Dispose()
    {
        ClearConnection();
        Platform?.Dispose();
        GC.SuppressFinalize(this);
    }
}
