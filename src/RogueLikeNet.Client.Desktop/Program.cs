using Engine.Platform.Sdl;
using RogueLikeNet.Client.Core;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Client.Desktop;

public class Program : BaseProgram
{
    protected override ISaveGameProvider CreateSaveProvider() => new SqliteSaveGameProvider("game.db");

    [STAThread]
    public static void Main(string[] args)
    {
        var program = new Program();

        using var platform = new SdlPlatform(
            "RogueLikeNet", 1280, 960,
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
