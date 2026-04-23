using Engine.Platform;
using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Client.Core.Screens;
using RogueLikeNet.Client.Core.Screens.Menus;
using RogueLikeNet.Core.Data;
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
        // Load game data
        var dataDir = DataDirectory.Find();
        if (dataDir != null)
            GameData.Instance = DataLoader.Load(dataDir);
        else
            GameData.Instance = DataLoader.LoadFromEmbeddedResources();

        GameData.Instance.LogLoadedData(Console.Out);

        Game.Initialize(platform);

        Game.PlayOfflineRequested += OnPlayOffline;
        Game.StartOfflineRequested += OnStartOffline;
        Game.StartOnlineRequested += OnStartOnline;
        Game.LoginOnlineRequested += OnLoginOnline;
        Game.ReturnToMenuRequested += OnReturnToMenu;
        Game.DebugSyncRequested += OnDebugSync;
        Game.NewOfflineGameRequested += OnNewOfflineGame;
        Game.LoadSlotRequested += OnLoadSlot;
        Game.AdminOnlineRequested += OnAdminOnline;
        Game.QuitRequested += () => _quitRequested = true;
    }

    private async void OnPlayOffline()
    {
        var saveProvider = CreateSaveProvider();
        var generator = GeneratorRegistry.Create(GeneratorRegistry.DefaultIndex, 0);
        _embeddedServer = new GameServer(0, generator, logWriter: Console.Out, saveProvider: saveProvider);
        if (Game.Debug.Enabled)
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

    private async void OnStartOffline(long seed, int classId, string playerName, int generatorIndex)
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
            if (Game.Debug.Enabled)
                ApplyDebugSettings();

            _embeddedServer.InitializeNewGame(playerName + "'s World", seed, generatorId);

            _embeddedServer.Start();

            var embeddedConnection = new EmbeddedServerConnection(_embeddedServer);
            _connection = embeddedConnection;
            Game.SetConnection(_connection);
            await _connection.ConnectAsync("embedded://localhost");
        }

        await _connection!.SendLoginAsync(new LoginMsg { PlayerName = playerName, Password = "" });
        await _connection!.SendClassSelectAsync(new ClassSelectMsg { ClassId = classId });

        while (!Game.IsFirstDeltaProcessed)
        {
            await Task.Delay(50);
        }

        if (Game.Debug.Enabled)
            ApplyDebugSettings();

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
        await _connection!.SendLoginAsync(new LoginMsg { PlayerName = ClassSelectScreen.DefaultPlayerName, Password = "" });

        while (!Game.IsFirstDeltaProcessed)
        {
            await Task.Delay(50);
        }

        if (Game.Debug.Enabled)
            ApplyDebugSettings();

        Game.TransitionToPlaying();
    }

    private async void OnStartOnline(int classId, string playerName)
    {
        // Connection already established by OnLoginOnline — just send class selection
        if (_connection == null || !_connection.IsConnected) return;

        Game.TransitionToConnecting();
        try
        {
            await _connection.SendClassSelectAsync(new ClassSelectMsg { ClassId = classId });

            while (!Game.IsFirstDeltaProcessed)
            {
                await Task.Delay(50);
            }

            Game.TransitionToPlaying();
        }
        catch (Exception ex)
        {
            Game.ClearConnection();
            _connection = null;
            Game.ShowConnectionError(ex.Message);
        }
    }

    private async void OnLoginOnline(string playerName, string password)
    {
        Game.TransitionToConnecting();

        try
        {
            var wsConnection = new WebSocketServerConnection();
            _connection = wsConnection;
            Game.SetConnection(_connection);
            await _connection.ConnectAsync("ws://localhost:5090/ws");

            var loginResponseTcs = new TaskCompletionSource<LoginResponseMsg>();
            void OnLoginResp(LoginResponseMsg resp) => loginResponseTcs.TrySetResult(resp);
            _connection.OnLoginResponse += OnLoginResp;

            await _connection.SendLoginAsync(new LoginMsg { PlayerName = playerName, Password = password });

            var response = await loginResponseTcs.Task;
            _connection.OnLoginResponse -= OnLoginResp;

            if (!response.Success)
            {
                Game.ClearConnection();
                await _connection.DisposeAsync();
                _connection = null;
                Game.ShowLoginError(response.ErrorMessage);
                return;
            }

            if (response.IsNewPlayer)
            {
                // New player — go to class selection
                Game.TransitionToClassSelect();
            }
            else
            {
                // Existing player — server already spawned, wait for snapshot
                while (!Game.IsFirstDeltaProcessed)
                {
                    await Task.Delay(50);
                }
                Game.TransitionToPlaying();
            }
        }
        catch (Exception ex)
        {
            Game.ClearConnection();
            _connection = null;
            Game.ShowLoginError(ex.Message);
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
        _embeddedServer.DebugFreeCrafting = debug.FreeCrafting;
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
