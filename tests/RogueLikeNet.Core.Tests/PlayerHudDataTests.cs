using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class PlayerHudDataTests
{
    [Fact]
    public void GetPlayerHudData_ReturnsValidData()
    {
        using var engine = new GameEngine(42);
        engine.EnsureChunkLoaded(0, 0);
        var (sx, sy) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, sx, sy, ClassIds.Warrior);

        var hud = engine.GetPlayerHudData(player);
        Assert.NotNull(hud);
        Assert.True(hud!.MaxHealth > 0);
        Assert.Equal(hud.Health, hud.MaxHealth);
        Assert.True(hud.Attack > 0);
        Assert.True(hud.Defense > 0);
        Assert.Equal(4, hud.SkillIds.Length);
    }
}
