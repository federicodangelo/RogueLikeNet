using System.Diagnostics;
using Engine.Platform;
using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.Screens;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Client.Core.Systems;
using RogueLikeNet.Core.Utilities;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core;

/// <summary>
/// Core game class — thin orchestrator that delegates to ScreenManager and extracted systems.
/// Owned by platform-specific Program.cs files.
/// </summary>
public sealed class RogueLikeGame : GameBase
{
    private readonly ClientGameState _gameState = new();
    private readonly ParticleSystem _particles = new();
    private readonly PerformanceMonitor _performance = new();
    private readonly ChatSystem _chat = new();
    private readonly ScreenShakeEffect _screenShake = new();
    private readonly NetworkMessageDrainer _networkDrainer = new();
    private readonly DebugSettings _debug = new();
    private readonly GameOptions _options = new();

    private readonly ScreenContext _ctx;
    private readonly ScreenManager _screenManager;
    private readonly ConnectingScreen _connectingScreen;
    private readonly SaveSlotScreen _saveSlotScreen;
    private readonly ServerAdminScreen _serverAdminScreen;
    private readonly ClassSelectScreen _classSelectScreen;
    private readonly LoginScreen _loginScreen;

    private IGameServerConnection? _connection;
    private long _lastFrameTicks;
    private int _lastSentVisibleChunks;
    private volatile bool _connectionClosed;

    public ClientGameState GameState => _gameState;
    public ScreenState CurrentScreen => _screenManager.CurrentState;
    public DebugSettings Debug => _debug;

    /// <summary>Fired when the player selects "Play Offline" from the main menu (sets up server before slot selection).</summary>
    public event Action? PlayOfflineRequested;

    /// <summary>Fired when the player selects "Play Offline" from the main menu.</summary>
    public event Action<long, int, string, int>? StartOfflineRequested;

    /// <summary>Fired when the player selects "Play Online" from the main menu.</summary>
    public event Action<int, string>? StartOnlineRequested;

    /// <summary>Fired when the player submits login credentials for online play.</summary>
    public event Action<string, string>? LoginOnlineRequested;

    /// <summary>Fired when the player selects "Return to Main Menu" from the pause menu.</summary>
    public event Action? ReturnToMenuRequested;

    /// <summary>Fired when the player selects "Quit" from the main menu.</summary>
    public event Action? QuitRequested;

    /// <summary>Fired when debug settings change at runtime and need syncing to the embedded server.</summary>
    public event Action? DebugSyncRequested;

    /// <summary>Fired when the player selects "Admin Online" from the main menu.</summary>
    public event Action? AdminOnlineRequested;

    /// <summary>Fired when the player chooses "New Game" from the save slot screen (slotName).</summary>
    public event Action<string>? NewOfflineGameRequested;

    /// <summary>Fired when the player selects an existing save slot to load (slotId).</summary>
    public event Action<string>? LoadSlotRequested;

    public bool IsFirstDeltaProcessed => _networkDrainer.FirstDeltaProcessed;



    public RogueLikeGame()
    {
        _ctx = new ScreenContext
        {
            GameState = _gameState,
            Particles = _particles,
            Chat = _chat,
            Performance = _performance,
            ScreenShake = _screenShake,
            Debug = _debug,
            Options = _options,
            RequestTransition = state => _screenManager!.TransitionTo(state),
            OnStartOffline = (seed, classId, name, genIndex) => StartOfflineRequested?.Invoke(seed, classId, name, genIndex),
            OnStartOnline = (classId, name) => StartOnlineRequested?.Invoke(classId, name),
            OnLoginOnline = (name, password) => LoginOnlineRequested?.Invoke(name, password),
            OnReturnToMenu = () => ReturnToMenuRequested?.Invoke(),
            OnQuit = () => QuitRequested?.Invoke(),
            OnPlayOffline = () => PlayOfflineRequested?.Invoke(),
            OnAdminOnline = () => AdminOnlineRequested?.Invoke(),
            DebugSyncRequested = () => DebugSyncRequested?.Invoke(),
        };

        // Create renderers
        var menuRenderer = new MenuRenderer();
        var worldRenderer = new GameWorldRenderer();
        var hudRenderer = new HudRenderer();
        var inventoryRenderer = new InventoryRenderer();
        var overlayRenderer = new OverlayRenderer();

        // Create screens
        var mainMenu = new MainMenuScreen(_ctx, menuRenderer);
        var newGame = new NewGameScreen(_ctx, menuRenderer);
        var classSelect = new ClassSelectScreen(_ctx, menuRenderer, mainMenu, newGame);
        var connecting = new ConnectingScreen(_ctx, menuRenderer);
        var playing = new PlayingScreen(_ctx, worldRenderer, hudRenderer, overlayRenderer);
        var inventory = new InventoryScreen(_ctx, worldRenderer, inventoryRenderer, overlayRenderer);
        var crafting = new CraftingScreen(_ctx, worldRenderer, overlayRenderer);
        var paused = new PausedScreen(_ctx, playing, menuRenderer);
        var help = new HelpScreen(_ctx, menuRenderer, playing);
        var options = new OptionsScreen(_ctx, menuRenderer, playing);
        var saveSlot = new SaveSlotScreen(_ctx, menuRenderer, newGame);
        var serverAdmin = new ServerAdminScreen(_ctx, menuRenderer, newGame);
        var login = new LoginScreen(_ctx, menuRenderer);

        var npcDialogue = new NpcDialogueScreen(_ctx, worldRenderer, overlayRenderer);
        var questLog = new QuestLogScreen(_ctx, worldRenderer, overlayRenderer);

        saveSlot.OnNewGameRequested = (slotName, _) => NewOfflineGameRequested?.Invoke(slotName);
        saveSlot.OnLoadSlotRequested = slotId => LoadSlotRequested?.Invoke(slotId);

        _connectingScreen = connecting;
        _saveSlotScreen = saveSlot;
        _serverAdminScreen = serverAdmin;
        _classSelectScreen = classSelect;
        _loginScreen = login;

        _screenManager = new ScreenManager(mainMenu, classSelect, connecting, playing, inventory, crafting, paused, help, options, saveSlot, serverAdmin, newGame, login, npcDialogue, questLog);
    }

    public void Initialize(IPlatform platform)
    {
        Platform = platform;
        _ctx.SpriteRenderer = SpriteRenderer;
        _ctx.Settings = Settings;

        // Load persisted options
        _options.Load(Settings);
        _options.LoadDebugEnabled(Settings, _debug);
    }

    public void SetConnection(IGameServerConnection connection)
    {
        _connection = connection;
        _networkDrainer.Reset();
        _connection.OnWorldDelta += OnNetworkWorldDelta;
        _connection.OnChatReceived += OnNetworkChatReceived;
        _connection.OnSaveGameResponse += OnNetworkSaveGameResponse;
        _connection.OnDisconnected += OnNetworkDisconnected;
        _ctx.Connection = _connection;
        _lastSentVisibleChunks = 0; // Force update of visible chunk count on next frame
        _connectionClosed = false;
    }

    public void ClearConnection()
    {
        if (_connection != null)
        {
            _connection.OnWorldDelta -= OnNetworkWorldDelta;
            _connection.OnChatReceived -= OnNetworkChatReceived;
            _connection.OnSaveGameResponse -= OnNetworkSaveGameResponse;
            _connection.OnDisconnected -= OnNetworkDisconnected;
            _connection = null;
            _ctx.Connection = null;
        }
        _connectionClosed = false;
    }

    public void TransitionToConnecting()
    {
        _connectingScreen.ClearError();
        _screenManager.TransitionTo(ScreenState.Connecting);
    }

    public void ShowConnectionError(string error)
    {
        _connectingScreen.SetError(error);
        _screenManager.TransitionTo(ScreenState.Connecting);
    }

    public void TransitionToPlaying()
    {
        _screenManager.TransitionTo(ScreenState.Playing);
    }

    public void TransitionToSaveSlotSelect()
    {
        _screenManager.TransitionTo(ScreenState.SaveSlotSelect);
    }

    public void TransitionToClassSelect()
    {
        _screenManager.TransitionTo(ScreenState.ClassSelect);
    }

    public void TransitionToLogin()
    {
        _loginScreen.OnEnter();
        _screenManager.TransitionTo(ScreenState.Login);
    }

    public void ShowLoginError(string error)
    {
        _loginScreen.SetError(error);
        _screenManager.TransitionTo(ScreenState.Login);
    }

    public void SetSaveSlots(SaveSlotInfoMsg[] slots)
    {
        _saveSlotScreen.SetSlots(slots);
    }

    public void SetSaveSlotStatus(string message, bool isError)
    {
        _saveSlotScreen.SetStatus(message, isError);
    }

    public void TransitionToMainMenu()
    {
        ClearConnection();
        _screenManager.TransitionTo(ScreenState.MainMenu);
        _gameState.Clear();
        _chat.Clear();
        _networkDrainer.Reset();
    }

    public void TransitionToServerAdmin()
    {
        _screenManager.TransitionTo(ScreenState.ServerAdmin);
    }

    public void RunFrame()
    {
        using var _ = TimeMeasurer.FromMethodName();

        var input = Input;
        var renderer = SpriteRenderer;

        input.BeginFrame();
        input.ProcessEvents();

        if (input.QuitRequested)
        {
            QuitRequested?.Invoke();
            input.EndFrame();
            return;
        }

        // Drain buffered network messages
        _networkDrainer.Drain(_gameState, _particles, _chat,
            onNpcDialogue: interaction =>
            {
                _screenManager!.OpenNpcDialogue(interaction);
            });
        _chat.DrainPendingMessages();

        if (_connectionClosed &&
            (_screenManager.CurrentState == ScreenState.Playing || _screenManager.CurrentState == ScreenState.Inventory || _screenManager.CurrentState == ScreenState.Crafting || _screenManager.CurrentState == ScreenState.NpcDialogue || _screenManager.CurrentState == ScreenState.QuestLog))
        {
            ReturnToMenuRequested?.Invoke();
        }

        // Frame timing
        long nowTicks = Stopwatch.GetTimestamp();
        float dt = _lastFrameTicks > 0
            ? (float)(nowTicks - _lastFrameTicks) / Stopwatch.Frequency
            : 1f / 60f;
        _lastFrameTicks = nowTicks;
        dt = Math.Clamp(dt, 0.001f, 0.1f);

        // Update performance metrics
        long bytesSent = _connection?.BytesSent ?? 0;
        long bytesReceived = _connection?.BytesReceived ?? 0;
        _performance.Update(bytesSent, bytesReceived);

        // Screen shake detection
        _screenShake.Update(_gameState.PlayerState?.Health ?? 0);

        // Input → Update → Render (delegated to ScreenManager)
        _screenManager.HandleInput(input);
        _screenManager.Update(dt);

        renderer.Update();
        renderer.BeginFrame();

        int totalCols = Math.Max(30, renderer.WindowWidth / AsciiDraw.TileWidth);
        int totalRows = Math.Max(15, renderer.WindowHeight / AsciiDraw.TileHeight);
        _screenManager.Render(renderer, totalCols, totalRows);

        // Compute visible chunk count and notify server if changed
        UpdateVisibleChunks(renderer.WindowWidth, renderer.WindowHeight);

        renderer.EndFrame();
        input.EndFrame();
    }

    // ── Viewport Tracking ───────────────────────────────────

    /// <summary>
    /// Computes how many distinct chunks are visible in the viewport and notifies
    /// the server whenever the count changes.
    /// </summary>
    private void UpdateVisibleChunks(int windowWidth, int windowHeight)
    {
        if (_connection == null) return;

        int visibleChunks = ComputeVisibleChunks(windowWidth, windowHeight, _debug);
        if (visibleChunks != _lastSentVisibleChunks)
        {
            _lastSentVisibleChunks = visibleChunks;
            _ = _connection.SendViewportInfoAsync(
                new ViewportInfoMsg { VisibleChunks = visibleChunks });
        }
    }

    /// <summary>
    /// Calculates the number of distinct chunks that can be visible given pixel
    /// dimensions and zoom settings. Accounts for the HUD column reservation.
    /// </summary>
    public static int ComputeVisibleChunks(int windowWidth, int windowHeight, DebugSettings debug)
    {
        int tileW = debug.EffectiveTileWidth;
        int tileH = debug.EffectiveTileHeight;

        // The game area excludes the HUD panel
        int gamePixelW = windowWidth - AsciiDraw.HudColumns * AsciiDraw.TileWidth;
        if (gamePixelW < tileW) gamePixelW = tileW;

        int visibleTileCols = gamePixelW / tileW;
        int visibleTileRows = windowHeight / tileH;

        // Number of chunk columns/rows: a visible span of N tiles on a 64-tile chunk
        // can straddle ceil(N / ChunkSize) + 1 chunks (player can be at chunk boundary)
        int chunkCols = visibleTileCols / RogueLikeNet.Core.World.Chunk.Size + 2;
        int chunkRows = visibleTileRows / RogueLikeNet.Core.World.Chunk.Size + 2;
        int maxSide = Math.Max(chunkCols, chunkRows);

        return Math.Clamp(maxSide * maxSide, 1, Protocol.ChunkTracker.MaxVisibleChunks);
    }

    // ── Network Callbacks (called from network thread) ──────

    private void OnNetworkWorldDelta(WorldDeltaMsg delta)
    {
        _performance.RecordDelta();
        _networkDrainer.EnqueueDelta(delta);
    }

    private void OnNetworkChatReceived(ChatMsg msg)
    {
        _chat.EnqueueMessage(msg);
    }

    private void OnNetworkSaveGameResponse(SaveGameResponseMsg msg)
    {
        // Route to whichever screen is active
        if (_screenManager.CurrentState == ScreenState.SaveSlotSelect)
        {
            _saveSlotScreen.SetSlots(msg.Slots);
            if (!string.IsNullOrEmpty(msg.Message))
                _saveSlotScreen.SetStatus(msg.Message, !msg.Success);
        }
        else if (_screenManager.CurrentState == ScreenState.ServerAdmin)
        {
            _serverAdminScreen.HandleSaveResponse(msg);
        }
    }

    private void OnNetworkDisconnected()
    {
        _connectionClosed = true;
    }

    public override void Dispose()
    {
        ClearConnection();
        Platform?.Dispose();
        GC.SuppressFinalize(this);
    }
}
