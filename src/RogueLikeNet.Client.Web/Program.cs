using Avalonia;
using Avalonia.Browser;
using RogueLikeNet.Client.Web;

internal sealed partial class Program
{
    private static async Task Main(string[] args)
    {
#pragma warning disable CA1416 // Validate platform compatibility
        await BuildAvaloniaApp()
            .StartBrowserAppAsync("out");
#pragma warning restore CA1416 // Validate platform compatibility
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .LogToTrace();
}
