using Engine.Platform.Sdl;
using RogueLikeNet.Client.Core;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Client.Desktop;

public class Program : BaseProgram, IDisposable
{
    private SqliteSaveGameProvider? _saveProvider;

    protected override ISaveGameProvider CreateSaveProvider()
    {
        if (_saveProvider == null)
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RogueLikeNet", "SaveGames"
            );

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var filePath = Path.Combine(dir, "game.db");

            _saveProvider = new SqliteSaveGameProvider(filePath, Console.Out);
        }

        return _saveProvider;
    }

    public void Dispose()
    {
        _saveProvider?.Dispose();
    }


    [STAThread]
    public static void Main(string[] args)
    {
        using var program = new Program();

        using var platform = new SdlPlatform(
            "RogueLikeNet", 1280, 960, "RogueLikeNet",
            new NullMusicProvider(), new NullSfxProvider());

        program.InitializeGame(platform);

        while (!program._quitRequested && !platform.InputManager.QuitRequested)
        {
            program.Game.RunFrame();
        }

        program.CleanupConnection();
        program.Game.Dispose();
    }
}
