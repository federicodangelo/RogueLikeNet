using System.Runtime.InteropServices.JavaScript;
using Engine.Platform.Web;
using RogueLikeNet.Client.Core;
using RogueLikeNet.Client.Web.Persistence;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Client.Web;

public partial class WebMain : BaseProgram
{
    private static readonly WebMain _instance = new();
    private static IndexedDbSaveGameProvider? _indexedDbProvider;

    protected override ISaveGameProvider CreateSaveProvider() => _indexedDbProvider!;

    public static async Task Main()
    {
        _indexedDbProvider = new IndexedDbSaveGameProvider();
        await _indexedDbProvider.InitializeAsync();

        var platform = new WebPlatform(
            "RogueLikeNet", 1280, 960,
            new NullMusicProvider(), new NullSfxProvider());

        _instance.InitializeGame(platform);
    }

    [JSExport]
    public static void RunOneFrame()
    {
        _instance.Game.RunFrame();
    }
}
