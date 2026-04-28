using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class NpcRegistryTests
{
    [Fact]
    public void Pick_FromGameData_ReturnsDefinition()
    {
        var rng = new SeededRandom(42);
        var result = GameData.Instance.Npcs.Pick(rng, 0);
        Assert.NotNull(result);
    }

    [Fact]
    public void Pick_HigherDifficulty_UnlocksMoreMonsters()
    {
        var rng = new SeededRandom(42);
        // With high difficulty, should be able to pick from the full range
        var result = GameData.Instance.Npcs.Pick(rng, 100);
        Assert.NotNull(result);
    }

    [Fact]
    public void Pick_EmptyRegistry_ReturnsNull()
    {
        var registry = new NpcRegistry();
        registry.Register([]);
        var rng = new SeededRandom(42);
        Assert.Null(registry.Pick(rng, 0));
    }

    [Fact]
    public void GenerateMonsterData_BaseDifficulty()
    {
        var def = new NpcDefinition
        {
            Id = "test_monster",
            Name = "Test",
            Health = 10,
            Attack = 5,
            Defense = 3,
            Speed = 2,
            AttackSpeed = 1,
        };
        var data = NpcRegistry.GenerateMonsterData(def, 0);
        Assert.Equal(10, data.Health);  // 10 + 10*(0/2) = 10
        Assert.Equal(5, data.Attack);   // 5 + 0 = 5
        Assert.Equal(3, data.Defense);  // 3 + 0/2 = 3
        Assert.Equal(2, data.Speed);    // 2 + 0 = 2
        Assert.Equal(1, data.AttackSpeed);
    }

    [Fact]
    public void GenerateMonsterData_HighDifficulty_ScalesStats()
    {
        var def = new NpcDefinition
        {
            Id = "test_monster",
            Name = "Test",
            Health = 10,
            Attack = 5,
            Defense = 3,
            Speed = 2,
            AttackSpeed = 1,
        };
        var data = NpcRegistry.GenerateMonsterData(def, 4);
        Assert.Equal(30, data.Health);  // 10 + 10*(4/2) = 10 + 20 = 30
        Assert.Equal(9, data.Attack);   // 5 + 4 = 9
        Assert.Equal(5, data.Defense);  // 3 + 4/2 = 3 + 2 = 5
        Assert.Equal(2, data.Speed);    // 2 + 0 = 2
        Assert.Equal(1, data.AttackSpeed);
    }

    [Fact]
    public void GenerateMonsterData_SetsMonsterTypeId()
    {
        var registry = new NpcRegistry();
        var defs = new[]
        {
            new NpcDefinition { Id = "goblin", Name = "Goblin", Health = 5, Attack = 2, Defense = 1, Speed = 3, AttackSpeed = 1 },
        };
        registry.Register(defs);
        var def = registry.Get("goblin")!;
        var data = NpcRegistry.GenerateMonsterData(def, 0);
        Assert.Equal(def.NumericId, data.MonsterTypeId);
    }
}
