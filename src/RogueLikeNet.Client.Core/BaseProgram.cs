using Engine.Platform;
using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Client.Core.Screens;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Protocol.Messages;
using RogueLikeNet.Server;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Client.Core;

public abstract class BaseProgram
{
    protected readonly RogueLikeGame Game = new();
    private IGameServerConnection? _connection;
    private GameServer? _embeddedServer;
    private string? _pendingSlotName;
    protected bool _quitRequested = false;

    protected abstract ISaveGameProvider CreateSaveProvider();

    protected void InitializeGame(IPlatform platform)
    {
        Game.Initialize(platform);

        Game.PlayOfflineRequested += debugMode => OnPlayOffline(debugMode);
        Game.StartOfflineRequested += (seed, classId, playerName, genIndex, debugMode) => OnStartOffline(seed, classId, playerName, genIndex, debugMode);
        Game.StartOnlineRequested += (classId, playerName) => OnStartOnline(classId, playerName);
        Game.ReturnToMenuRequested += OnReturnToMenu;
        Game.DebugSyncRequested += OnDebugSync;
        Game.NewOfflineGameRequested += slotName => OnNewOfflineGame(slotName);
        Game.LoadSlotRequested += slotId => OnLoadSlot(slotId);
        Game.AdminOnlineRequested += OnAdminOnline;
        Game.QuitRequested += () => _quitRequested = true;
    }

    private async void OnPlayOffline(bool debugMode)
    {
        var saveProvider = CreateSaveProvider();
        var generator = GeneratorRegistry.Create(GeneratorRegistry.DefaultIndex, 0);
        _embeddedServer = new GameServer(0, generator, logWriter: Console.Out, saveProvider: saveProvider);

        if (debugMode)
            ApplyDebugSettings();

        _embeddedServer.Start();

        var embeddedConnection = new EmbeddedServerConnection(_embeddedServer);
        _connection = embeddedConnection;
        Game.SetConnection(_connection);
        await _connection.ConnectAsync("embedded://localhost");

        Game.TransitionToSaveSlotSelect();
    }

    private void OnNewOfflineGame(string slotName)
    {
        _pendingSlotName = slotName;
        Game.TransitionToClassSelect();
    }

    private async void OnStartOffline(long seed, int classId, string playerName, int generatorIndex, bool debugMode)
    {
        Game.TransitionToConnecting();

        var generatorId = GeneratorRegistry.GetId(generatorIndex);

        if (_pendingSlotName != null)
        {
            var slotName = _pendingSlotName;
            _pendingSlotName = null;
            await _connection!.SendSaveGameCommandAsync(new SaveGameCommandMsg
            {
                Action = SaveGameAction.New,
                SlotName = slotName,
                Seed = seed,
                GeneratorId = generatorId,
            });

            await _connection.ReconnectAsync();
        }
        else
        {
            var saveProvider = CreateSaveProvider();
            var generator = GeneratorRegistry.Create(generatorIndex, seed);
            _embeddedServer = new GameServer(seed, generator, logWriter: Console.Out, saveProvider: saveProvider);
            _embeddedServer.InitializeNewGame(playerName + "'s World", seed, generatorId);

            if (debugMode)
                ApplyDebugSettings();

            _embeddedServer.Start();

            var embeddedConnection = new EmbeddedServerConnection(_embeddedServer);
            _connection = embeddedConnection;
            Game.SetConnection(_connection);
            await _connection.ConnectAsync("embedded://localhost");
        }

        await _connection!.SendLoginAsync(new LoginMsg { ClassId = classId, PlayerName = playerName });

        while (!Game.IsFirstDeltaProcessed)
        {
            await Task.Delay(50);
        }

        if (debugMode)
            Game.GameState.DebugSeeAll = Game.Debug.VisibilityOff;

        Game.TransitionToPlaying();
    }

    private async void OnLoadSlot(string slotId)
    {
        Game.TransitionToConnecting();

        await _connection!.SendSaveGameCommandAsync(new SaveGameCommandMsg
        {
            Action = SaveGameAction.Load,
            SlotId = slotId,
        });

        await _connection.ReconnectAsync();

        // We have to use the default player name since it needs to match the name in the savegame
        await _connection!.SendLoginAsync(new LoginMsg { ClassId = 0, PlayerName = ClassSelectScreen.DefaultPlayerName });

        while (!Game.IsFirstDeltaProcessed)
        {
            await Task.Delay(50);
        }

        if (Game.Debug.Enabled)
            Game.GameState.DebugSeeAll = Game.Debug.VisibilityOff;

        Game.TransitionToPlaying();
    }

    private async void OnStartOnline(int classId, string playerName)
    {
        Game.TransitionToConnecting();

        try
        {
            var wsConnection = new WebSocketServerConnection();
            _connection = wsConnection;
            Game.SetConnection(_connection);
            await _connection.ConnectAsync("ws://localhost:5090/ws");
            await _connection.SendLoginAsync(new LoginMsg { ClassId = classId, PlayerName = playerName });
            Game.TransitionToPlaying();
        }
        catch (Exception ex)
        {
            Game.ClearConnection();
            _connection = null;
            Game.ShowConnectionError(ex.Message);
        }
    }

    private async void OnAdminOnline()
    {
        Game.TransitionToConnecting();

        try
        {
            var wsConnection = new WebSocketServerConnection();
            _connection = wsConnection;
            Game.SetConnection(_connection);
            await _connection.ConnectAsync("ws://localhost:5090/ws");
            Game.TransitionToServerAdmin();
        }
        catch (Exception ex)
        {
            Game.ClearConnection();
            _connection = null;
            Game.ShowConnectionError(ex.Message);
        }
    }

    private void OnReturnToMenu()
    {
        Game.TransitionToMainMenu();
        _pendingSlotName = null;
        CleanupConnection();
    }

    private void OnDebugSync()
    {
        if (_embeddedServer != null)
            ApplyDebugSettings();
    }

    private void ApplyDebugSettings()
    {
        if (_embeddedServer == null) return;
        var debug = Game.Debug;
        _embeddedServer.DebugNoCollision = debug.CollisionsOff;
        _embeddedServer.DebugInvulnerable = debug.Invulnerable;
        _embeddedServer.DebugMaxSpeed = debug.MaxSpeed;
        _embeddedServer.DebugVisibilityOff = debug.VisibilityOff;
        _embeddedServer.DebugGiveResources = debug.Enabled;
        Game.GameState.DebugSeeAll = debug.VisibilityOff;
    }

    protected void CleanupConnection()
    {
        // Destroy server before connection to ensure that player data is saved before disconnecting
        _embeddedServer?.Dispose();
        _embeddedServer = null;
        _connection?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _connection = null;
    }

    protected sealed class NullMusicProvider : IMusicProvider
    {
        public string CurrentTheme => "";
        public void SetTheme(string theme) { }
        public void Generate(float[] buffer, int frames) { }
    }

    protected sealed class NullSfxProvider : ISfxProvider
    {
        public bool TryGetBuffer(string sfx, out float[] buffer) { buffer = []; return false; }
    }
}
