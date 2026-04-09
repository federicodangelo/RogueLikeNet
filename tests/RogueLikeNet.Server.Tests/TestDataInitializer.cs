using System.Runtime.CompilerServices;
using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Server.Tests;

internal static class TestDataInitializer
{
    [ModuleInitializer]
    internal static void Init()
    {
        if (GameData.Instance.Items.Count > 0) return;

        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "data");
            if (Directory.Exists(candidate))
            {
                GameData.Instance = DataLoader.Load(candidate);
                return;
            }
            dir = Path.GetDirectoryName(dir)!;
        }

        throw new InvalidOperationException("Could not find /data directory for test data loading");
    }
}
