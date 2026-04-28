using RogueLikeNet.Server;

namespace RogueLikeNet.Server.Tests;

public class ServerRuntimeOptionsTests
{
    [Fact]
    public void FromLookup_WithoutValues_UsesDefaults()
    {
        var options = ServerRuntimeOptions.FromLookup(_ => null);

        Assert.Equal(ServerRuntimeOptions.DefaultWorldSeed, options.WorldSeed);
        Assert.Equal(ServerRuntimeOptions.DefaultDatabasePath, options.DatabasePath);
    }

    [Fact]
    public void FromLookup_ReadsNamespacedValues()
    {
        var values = new Dictionary<string, string?>
        {
            ["RogueLikeNet:WorldSeed"] = "987654321",
            ["RogueLikeNet:DatabasePath"] = "custom.db",
        };

        var options = ServerRuntimeOptions.FromLookup(key => values.GetValueOrDefault(key));

        Assert.Equal(987654321, options.WorldSeed);
        Assert.Equal("custom.db", options.DatabasePath);
    }

    [Fact]
    public void FromLookup_ReadsEnvironmentStyleAliases()
    {
        var values = new Dictionary<string, string?>
        {
            ["ROGUELIKENET_WORLD_SEED"] = "42",
            ["ROGUELIKENET_DATABASE_PATH"] = "saves/test.db",
        };

        var options = ServerRuntimeOptions.FromLookup(key => values.GetValueOrDefault(key));

        Assert.Equal(42, options.WorldSeed);
        Assert.Equal("saves/test.db", options.DatabasePath);
    }

    [Fact]
    public void FromLookup_InvalidSeed_ThrowsClearError()
    {
        var values = new Dictionary<string, string?>
        {
            ["WorldSeed"] = "not-a-number",
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ServerRuntimeOptions.FromLookup(key => values.GetValueOrDefault(key)));
        Assert.Contains("Invalid world seed", ex.Message);
    }
}
