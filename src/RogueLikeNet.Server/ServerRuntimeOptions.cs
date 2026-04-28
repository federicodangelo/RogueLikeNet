using Microsoft.Extensions.Configuration;

namespace RogueLikeNet.Server;

public sealed class ServerRuntimeOptions
{
    public const long DefaultWorldSeed = 12345;
    public const string DefaultDatabasePath = "game.db";

    public long WorldSeed { get; init; } = DefaultWorldSeed;
    public string DatabasePath { get; init; } = DefaultDatabasePath;

    public static ServerRuntimeOptions FromConfiguration(IConfiguration configuration)
        => FromLookup(key => configuration[key]);

    public static ServerRuntimeOptions FromLookup(Func<string, string?> getValue)
    {
        string? seedValue = FirstValue(getValue,
            "RogueLikeNet:WorldSeed",
            "WorldSeed",
            "ROGUELIKENET_WORLD_SEED");
        string? databasePath = FirstValue(getValue,
            "RogueLikeNet:DatabasePath",
            "DatabasePath",
            "ROGUELIKENET_DATABASE_PATH");

        return new ServerRuntimeOptions
        {
            WorldSeed = ParseWorldSeed(seedValue),
            DatabasePath = string.IsNullOrWhiteSpace(databasePath) ? DefaultDatabasePath : databasePath.Trim(),
        };
    }

    private static long ParseWorldSeed(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DefaultWorldSeed;
        if (long.TryParse(value.Trim(), out long seed)) return seed;

        throw new InvalidOperationException($"Invalid world seed '{value}'. Configure a valid 64-bit integer.");
    }

    private static string? FirstValue(Func<string, string?> getValue, params string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            string? value = getValue(keys[i]);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return null;
    }
}
