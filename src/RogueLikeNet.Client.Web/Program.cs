using System.Runtime.InteropServices.JavaScript;
using Engine.Platform.Web;
using RogueLikeNet.Client.Core;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Client.Web;

public partial class WebMain : BaseProgram
{
    private static readonly WebMain _instance = new();

    protected override ISaveGameProvider CreateSaveProvider() => new InMemorySaveGameProvider();

    public static async Task Main()
    {
        var platform = new WebPlatform(
            "RogueLikeNet", 1280, 960,
            new NullMusicProvider(), new NullSfxProvider());

        _instance.InitializeGame(platform);

        await Task.CompletedTask;
    }

    [JSExport]
    public static void RunOneFrame()
    {
        _instance.Game.RunFrame();
    }
}
