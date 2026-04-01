using System.Runtime.InteropServices.JavaScript;
using Engine.Platform;
using Engine.Platform.Web;
using RogueLikeNet.Client.Core;
using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Protocol.Messages;
using RogueLikeNet.Server;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Client.Web;

public partial class WebMain
{
    private static RogueLikeGame _game = new();
    private static IGameServerConnection? _connection;
    private static GameServer? _embeddedServer;
    private static string? _pendingSlotName;

    public static async Task Main()
    {
        var platform = new WebPlatform(
            "RogueLikeNet", 1280, 960,
            new NullMusicProvider(), new NullSfxProvider());

        _game.Initialize(platform);

        _game.PlayOfflineRequested += debugMode => OnPlayOffline(debugMode);
        _game.StartOfflineRequested += (seed, classId, playerName, genIndex, debugMode) => OnStartOffline(seed, classId, playerName, genIndex, debugMode);
        _game.StartOnlineRequested += (classId, playerName) => OnStartOnline(classId, playerName);
        _game.ReturnToMenuRequested += OnReturnToMenu;
        _game.DebugSyncRequested += OnDebugSync;
        _game.NewOfflineGameRequested += slotName => OnNewOfflineGame(slotName);
        _game.LoadSlotRequested += slotId => OnLoadSlot(slotId);
        _game.AdminOnlineRequested += OnAdminOnline;
        // Web platform cannot quit — QuitRequested is ignored

        await Task.CompletedTask;
    }

    [JSExport]
    public static void RunOneFrame()
    {
        _game?.RunFrame();
    }

    private static async void OnPlayOffline(bool debugMode)
    {
        var saveProvider = new InMemorySaveGameProvider();
        var generator = GeneratorRegistry.Create(GeneratorRegistry.DefaultIndex, 0);
        _embeddedServer = new GameServer(0, generator, logWriter: Console.Out, saveProvider: saveProvider);

        if (debugMode)
            ApplyDebugSettings();

        _embeddedServer.Start();

        var embeddedConnection = new EmbeddedServerConnection(_embeddedServer);
        _connection = embeddedConnection;
        _game.SetConnection(_connection);
        await _connection.ConnectAsync("embedded://localhost");

        _game.TransitionToSaveSlotSelect();
    }

    private static void OnNewOfflineGame(string slotName)
    {
        _pendingSlotName = slotName;
        _game.TransitionToClassSelect();
    }

    private static async void OnStartOffline(long seed, int classId, string playerName, int generatorIndex, bool debugMode)
    {
        _game.TransitionToConnecting();

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
        }
        else
        {
            var saveProvider = new InMemorySaveGameProvider();
            var generator = GeneratorRegistry.Create(generatorIndex, seed);
            _embeddedServer = new GameServer(seed, generator, logWriter: Console.Out, saveProvider: saveProvider);
            _embeddedServer.InitializeNewGame(playerName + "'s World", seed, generatorId);

            if (debugMode)
                ApplyDebugSettings();

            _embeddedServer.Start();

            var embeddedConnection = new EmbeddedServerConnection(_embeddedServer);
            _connection = embeddedConnection;
            _game.SetConnection(_connection);
            await _connection.ConnectAsync("embedded://localhost");
        }

        await _connection!.SendLoginAsync(new LoginMsg { ClassId = classId, PlayerName = playerName });

        while (!_game.IsFirstDeltaProcessed)
        {
            await Task.Delay(50);
        }

        if (debugMode)
            _game.GameState.DebugSeeAll = _game.Debug.VisibilityOff;

        _game.TransitionToPlaying();
    }

    private static async void OnLoadSlot(string slotId)
    {
        _game.TransitionToConnecting();

        await _connection!.SendSaveGameCommandAsync(new SaveGameCommandMsg
        {
            Action = SaveGameAction.Load,
            SlotId = slotId,
        });

        await _connection!.SendLoginAsync(new LoginMsg { ClassId = 0, PlayerName = "Player" });

        while (!_game.IsFirstDeltaProcessed)
        {
            await Task.Delay(50);
        }

        if (_game.Debug.Enabled)
            _game.GameState.DebugSeeAll = _game.Debug.VisibilityOff;

        _game.TransitionToPlaying();
    }

    private static async void OnStartOnline(int classId, string playerName)
    {
        _game.TransitionToConnecting();

        try
        {
            var wsConnection = new WebSocketServerConnection();
            _connection = wsConnection;
            _game.SetConnection(_connection);
            await _connection.ConnectAsync("ws://localhost:5090/ws");
            await _connection.SendLoginAsync(new LoginMsg { ClassId = classId, PlayerName = playerName });
            _game.TransitionToPlaying();
        }
        catch (Exception ex)
        {
            _game.ClearConnection();
            _connection = null;
            _game.ShowConnectionError(ex.Message);
        }
    }

    private static async void OnAdminOnline()
    {
        _game.TransitionToConnecting();

        try
        {
            var wsConnection = new WebSocketServerConnection();
            _connection = wsConnection;
            _game.SetConnection(_connection);
            await _connection.ConnectAsync("ws://localhost:5090/ws");
            _game.TransitionToServerAdmin();
        }
        catch (Exception ex)
        {
            _game.ClearConnection();
            _connection = null;
            _game.ShowConnectionError(ex.Message);
        }
    }

    private static void OnReturnToMenu()
    {
        _game.TransitionToMainMenu();
        _pendingSlotName = null;

        _connection?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _connection = null;
        _embeddedServer?.Dispose();
        _embeddedServer = null;
    }

    private static void OnDebugSync()
    {
        if (_embeddedServer != null && _game != null)
            ApplyDebugSettings();
    }

    private static void ApplyDebugSettings()
    {
        if (_embeddedServer == null || _game == null) return;
        var debug = _game.Debug;
        _embeddedServer.DebugNoCollision = debug.CollisionsOff;
        _embeddedServer.DebugInvulnerable = debug.Invulnerable;
        _embeddedServer.DebugMaxSpeed = debug.MaxSpeed;
        _embeddedServer.DebugVisibilityOff = debug.VisibilityOff;
        _embeddedServer.DebugGiveResources = debug.Enabled;
        _game.GameState.DebugSeeAll = debug.VisibilityOff;
    }

    private sealed class NullMusicProvider : IMusicProvider
    {
        public string CurrentTheme => "";
        public void SetTheme(string theme) { }
        public void Generate(float[] buffer, int frames) { }
    }

    private sealed class NullSfxProvider : ISfxProvider
    {
        public bool TryGetBuffer(string sfx, out float[] buffer) { buffer = []; return false; }
    }
}
