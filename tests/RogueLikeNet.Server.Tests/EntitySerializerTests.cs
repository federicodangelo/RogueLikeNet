using Arch.Core;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Server.Persistence;

namespace RogueLikeNet.Server.Tests;

public class EntitySerializerTests : IDisposable
{
    private static readonly BspDungeonGenerator _gen = new(42);
    private readonly GameEngine _engine;
    private const int Z = Position.DefaultZ;

    public EntitySerializerTests()
    {
        _engine = new GameEngine(42, _gen);
        _engine.EnsureChunkLoaded(0, 0, Z);
    }

    public void Dispose() => _engine.Dispose();

    [Fact]
    public void SerializeEmpty_ReturnsEmptyArray()
    {
        // Chunk (99,99) has no entities
        var json = EntitySerializer.SerializeEntities(_engine.EcsWorld, 99, 99, Z);
        Assert.Equal("[]", json);
    }

    [Fact]
    public void Monster_RoundTrip_PreservesData()
    {
        var md = new MonsterData { MonsterTypeId = 7, Health = 50, Attack = 12, Defense = 4, Speed = 8 };
        var entity = _engine.SpawnMonster(1, 2, Z, md);

        // Tweak runtime state so we can verify it survives the round-trip
        ref var hp = ref _engine.EcsWorld.Get<Health>(entity);
        hp.Current = 30;
        ref var ai = ref _engine.EcsWorld.Get<AIState>(entity);
        ai.StateId = 2;
        ai.PatrolX = 10;
        ai.PatrolY = 20;
        ai.AlertCooldown = 5;
        ref var move = ref _engine.EcsWorld.Get<MoveDelay>(entity);
        move.Current = 3;
        ref var atk = ref _engine.EcsWorld.Get<AttackDelay>(entity);
        atk.Current = 7;

        var json = EntitySerializer.SerializeEntities(_engine.EcsWorld, 0, 0, Z);
        Assert.Contains("\"Type\":\"Monster\"", json);

        // Deserialize into a fresh engine
        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(0, 0, Z);
        EntitySerializer.DeserializeEntities(json, engine2);

        // Find the deserialized monster
        var query = new QueryDescription().WithAll<Position, MonsterData, Health, AIState, MoveDelay, AttackDelay>();
        Entity found = Entity.Null;
        engine2.EcsWorld.Query(in query, (Entity e, ref Position p) =>
        {
            if (p.X == 1 && p.Y == 2 && p.Z == Z) found = e;
        });
        Assert.NotEqual(Entity.Null, found);

        var md2 = engine2.EcsWorld.Get<MonsterData>(found);
        Assert.Equal(7, md2.MonsterTypeId);
        Assert.Equal(50, md2.Health);
        Assert.Equal(12, md2.Attack);
        Assert.Equal(4, md2.Defense);
        Assert.Equal(8, md2.Speed);

        var hp2 = engine2.EcsWorld.Get<Health>(found);
        Assert.Equal(30, hp2.Current);
        Assert.Equal(50, hp2.Max);

        var ai2 = engine2.EcsWorld.Get<AIState>(found);
        Assert.Equal(2, ai2.StateId);
        Assert.Equal(10, ai2.PatrolX);
        Assert.Equal(20, ai2.PatrolY);
        Assert.Equal(5, ai2.AlertCooldown);

        var move2 = engine2.EcsWorld.Get<MoveDelay>(found);
        Assert.Equal(3, move2.Current);

        var atk2 = engine2.EcsWorld.Get<AttackDelay>(found);
        Assert.Equal(7, atk2.Current);
    }

    [Fact]
    public void GroundItem_RoundTrip_PreservesData()
    {
        var itemData = new ItemData
        {
            ItemTypeId = 3,
            Rarity = 2,
            BonusAttack = 5,
            BonusDefense = 3,
            BonusHealth = 10,
            StackCount = 4,
        };
        _engine.SpawnItemOnGround(itemData, 4, 5, Z);

        var json = EntitySerializer.SerializeEntities(_engine.EcsWorld, 0, 0, Z);
        Assert.Contains("\"Type\":\"GroundItem\"", json);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(0, 0, Z);
        EntitySerializer.DeserializeEntities(json, engine2);

        var query = new QueryDescription().WithAll<Position, ItemData, TileAppearance>();
        Entity found = Entity.Null;
        engine2.EcsWorld.Query(in query, (Entity e, ref Position p) =>
        {
            if (p.X == 4 && p.Y == 5 && p.Z == Z) found = e;
        });
        Assert.NotEqual(Entity.Null, found);

        var id2 = engine2.EcsWorld.Get<ItemData>(found);
        Assert.Equal(3, id2.ItemTypeId);
        Assert.Equal(2, id2.Rarity);
        Assert.Equal(5, id2.BonusAttack);
        Assert.Equal(3, id2.BonusDefense);
        Assert.Equal(10, id2.BonusHealth);
        Assert.Equal(4, id2.StackCount);
    }

    [Fact]
    public void ResourceNode_RoundTrip_PreservesData()
    {
        var entity = _engine.EcsWorld.Create(
            new Position(3, 3, Z),
            new Health { Current = 80, Max = 100 },
            new CombatStats(0, 5, 0),
            new TileAppearance(42, 0xFF0000, 0x000000),
            new ResourceNodeData { ResourceItemTypeId = 9, MinDrop = 1, MaxDrop = 3 },
            new AttackDelay { Interval = 10, Current = 2 }
        );

        var json = EntitySerializer.SerializeEntities(_engine.EcsWorld, 0, 0, Z);
        Assert.Contains("\"Type\":\"ResourceNode\"", json);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(0, 0, Z);
        EntitySerializer.DeserializeEntities(json, engine2);

        var query = new QueryDescription().WithAll<Position, ResourceNodeData, Health>();
        Entity found = Entity.Null;
        engine2.EcsWorld.Query(in query, (Entity e, ref Position p) =>
        {
            if (p.X == 3 && p.Y == 3 && p.Z == Z) found = e;
        });
        Assert.NotEqual(Entity.Null, found);

        var rnd = engine2.EcsWorld.Get<ResourceNodeData>(found);
        Assert.Equal(9, rnd.ResourceItemTypeId);
        Assert.Equal(1, rnd.MinDrop);
        Assert.Equal(3, rnd.MaxDrop);

        var hp = engine2.EcsWorld.Get<Health>(found);
        Assert.Equal(80, hp.Current);
        Assert.Equal(100, hp.Max);

        var atk = engine2.EcsWorld.Get<AttackDelay>(found);
        Assert.Equal(10, atk.Interval);
        Assert.Equal(2, atk.Current);
    }

    [Fact]
    public void Element_RoundTrip_PreservesData()
    {
        _engine.EcsWorld.Create(
            new Position(6, 7, Z),
            new TileAppearance(99, 0xAABBCC, 0x112233)
        );

        var json = EntitySerializer.SerializeEntities(_engine.EcsWorld, 0, 0, Z);
        Assert.Contains("\"Type\":\"Element\"", json);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(0, 0, Z);
        EntitySerializer.DeserializeEntities(json, engine2);

        var query = new QueryDescription().WithAll<Position, TileAppearance>()
            .WithNone<MonsterData, ItemData, ResourceNodeData, Health, LightSource, TownNpcTag>();
        Entity found = Entity.Null;
        engine2.EcsWorld.Query(in query, (Entity e, ref Position p) =>
        {
            if (p.X == 6 && p.Y == 7 && p.Z == Z) found = e;
        });
        Assert.NotEqual(Entity.Null, found);

        var ta = engine2.EcsWorld.Get<TileAppearance>(found);
        Assert.Equal(99, ta.GlyphId);
        Assert.Equal(0xAABBCC, ta.FgColor);
        Assert.Equal(0x112233, ta.BgColor);
    }

    [Fact]
    public void ElementWithLight_RoundTrip_PreservesData()
    {
        _engine.EcsWorld.Create(
            new Position(8, 9, Z),
            new TileAppearance(55, 0xFFCC66, 0x000000),
            new LightSource(5, 0xFFCC66)
        );

        var json = EntitySerializer.SerializeEntities(_engine.EcsWorld, 0, 0, Z);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(0, 0, Z);
        EntitySerializer.DeserializeEntities(json, engine2);

        var query = new QueryDescription().WithAll<Position, TileAppearance, LightSource>();
        Entity found = Entity.Null;
        engine2.EcsWorld.Query(in query, (Entity e, ref Position p) =>
        {
            if (p.X == 8 && p.Y == 9 && p.Z == Z) found = e;
        });
        Assert.NotEqual(Entity.Null, found);

        var ls = engine2.EcsWorld.Get<LightSource>(found);
        Assert.Equal(5, ls.Radius);
        Assert.Equal(0xFFCC66, ls.ColorRgb);
    }

    [Fact]
    public void TownNpc_RoundTrip_PreservesData()
    {
        _engine.EcsWorld.Create(
            new Position(2, 8, Z),
            new Health { Current = 9999, Max = 9999 },
            new CombatStats(0, 999, 3),
            new TileAppearance(200, 0x00FF00, 0x000000),
            new AIState { StateId = 1 },
            new MoveDelay { Interval = 5, Current = 2 },
            new AttackDelay { Interval = 10, Current = 0 },
            new TownNpcTag { Name = "Blacksmith", TownCenterX = 5, TownCenterY = 10, WanderRadius = 3 }
        );

        var json = EntitySerializer.SerializeEntities(_engine.EcsWorld, 0, 0, Z);
        Assert.Contains("\"Type\":\"TownNpc\"", json);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(0, 0, Z);
        EntitySerializer.DeserializeEntities(json, engine2);

        var query = new QueryDescription().WithAll<Position, TownNpcTag, Health>();
        Entity found = Entity.Null;
        engine2.EcsWorld.Query(in query, (Entity e, ref Position p) =>
        {
            if (p.X == 2 && p.Y == 8 && p.Z == Z) found = e;
        });
        Assert.NotEqual(Entity.Null, found);

        var npc = engine2.EcsWorld.Get<TownNpcTag>(found);
        Assert.Equal("Blacksmith", npc.Name);
        Assert.Equal(5, npc.TownCenterX);
        Assert.Equal(10, npc.TownCenterY);
        Assert.Equal(3, npc.WanderRadius);

        var hp = engine2.EcsWorld.Get<Health>(found);
        Assert.Equal(9999, hp.Current);
        Assert.Equal(9999, hp.Max);

        var ai = engine2.EcsWorld.Get<AIState>(found);
        Assert.Equal(1, ai.StateId);

        var move = engine2.EcsWorld.Get<MoveDelay>(found);
        Assert.Equal(5, move.Interval);
        Assert.Equal(2, move.Current);
    }

    [Fact]
    public void MultipleEntityTypes_RoundTrip()
    {
        // Spawn one of each type
        _engine.SpawnMonster(1, 1, Z, new MonsterData { MonsterTypeId = 1, Health = 10, Attack = 3, Defense = 1, Speed = 5 });
        _engine.SpawnItemOnGround(new ItemData { ItemTypeId = 2, StackCount = 1 }, 2, 2, Z);
        _engine.EcsWorld.Create(
            new Position(3, 3, Z),
            new Health { Current = 50, Max = 50 },
            new CombatStats(0, 5, 0),
            new TileAppearance(10, 0, 0),
            new ResourceNodeData { ResourceItemTypeId = 4, MinDrop = 1, MaxDrop = 2 },
            new AttackDelay { Interval = 8, Current = 0 }
        );
        _engine.EcsWorld.Create(new Position(4, 4, Z), new TileAppearance(20, 0, 0));
        _engine.EcsWorld.Create(new Position(5, 5, Z), new TileAppearance(30, 0, 0), new LightSource(3, 0xFFFFFF));
        _engine.EcsWorld.Create(
            new Position(6, 6, Z),
            new Health { Current = 9999, Max = 9999 },
            new CombatStats(0, 999, 3),
            new TileAppearance(40, 0, 0),
            new AIState(),
            new MoveDelay { Interval = 5 },
            new AttackDelay { Interval = 10 },
            new TownNpcTag { Name = "Vendor", TownCenterX = 6, TownCenterY = 6, WanderRadius = 2 }
        );

        var json = EntitySerializer.SerializeEntities(_engine.EcsWorld, 0, 0, Z);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(0, 0, Z);
        EntitySerializer.DeserializeEntities(json, engine2);

        // Count each type
        int monsters = 0, items = 0, nodes = 0, elements = 0, lights = 0, npcs = 0;

        var mq = new QueryDescription().WithAll<MonsterData>();
        engine2.EcsWorld.Query(in mq, (Entity _) => monsters++);

        var iq = new QueryDescription().WithAll<ItemData>().WithNone<MonsterData, ResourceNodeData>();
        engine2.EcsWorld.Query(in iq, (Entity _) => items++);

        var rq = new QueryDescription().WithAll<ResourceNodeData>();
        engine2.EcsWorld.Query(in rq, (Entity _) => nodes++);

        var eq = new QueryDescription().WithAll<TileAppearance>().WithNone<MonsterData, ItemData, ResourceNodeData, Health, LightSource, TownNpcTag>();
        engine2.EcsWorld.Query(in eq, (Entity _) => elements++);

        var lq = new QueryDescription().WithAll<TileAppearance, LightSource>().WithNone<MonsterData, ItemData, ResourceNodeData, Health, TownNpcTag>();
        engine2.EcsWorld.Query(in lq, (Entity _) => lights++);

        var nq = new QueryDescription().WithAll<TownNpcTag>();
        engine2.EcsWorld.Query(in nq, (Entity _) => npcs++);

        // At least the ones we spawned (generator may also place entities in the chunk)
        Assert.True(monsters >= 1, $"Expected >= 1 monster, got {monsters}");
        Assert.True(items >= 1, $"Expected >= 1 item, got {items}");
        Assert.True(nodes >= 1, $"Expected >= 1 resource node, got {nodes}");
        Assert.True(elements >= 1, $"Expected >= 1 element, got {elements}");
        Assert.True(lights >= 1, $"Expected >= 1 light element, got {lights}");
        Assert.True(npcs >= 1, $"Expected >= 1 NPC, got {npcs}");
    }

    [Fact]
    public void Deserialize_EmptyString_DoesNothing()
    {
        EntitySerializer.DeserializeEntities("", _engine);
        EntitySerializer.DeserializeEntities("[]", _engine);
        // No exception thrown — pass
    }

    [Fact]
    public void Deserialize_NullString_DoesNothing()
    {
        EntitySerializer.DeserializeEntities(null!, _engine);
        // No exception thrown — pass
    }

    [Fact]
    public void OnlySerializesEntitiesInRequestedChunk()
    {
        // Spawn in chunk (0,0)
        _engine.SpawnMonster(1, 1, Z, new MonsterData { MonsterTypeId = 1, Health = 10, Attack = 1, Defense = 1, Speed = 1 });

        // Serialize chunk (1,1) — should not contain the monster at (1,1) since that's in chunk (0,0)
        var json = EntitySerializer.SerializeEntities(_engine.EcsWorld, 1, 1, Z);

        Assert.DoesNotContain("\"Type\":\"Monster\"", json);
    }

    [Fact]
    public void Serialize_SkipsDeadEntities()
    {
        var entity = _engine.SpawnMonster(1, 1, Z, new MonsterData { MonsterTypeId = 9999, Health = 10, Attack = 1, Defense = 1, Speed = 1 });

        // Kill the monster
        _engine.EcsWorld.Add<DeadTag>(entity);

        var json = EntitySerializer.SerializeEntities(_engine.EcsWorld, 0, 0, Z);
        Assert.DoesNotContain("\"MonsterTypeId\":9999", json);
    }

    [Fact]
    public void TownNpc_NullName_SerializesAsEmpty()
    {
        _engine.EcsWorld.Create(
            new Position(2, 2, Z),
            new Health { Current = 100, Max = 100 },
            new CombatStats(0, 999, 3),
            new TileAppearance(200, 0, 0),
            new AIState(),
            new MoveDelay { Interval = 5 },
            new AttackDelay { Interval = 10 },
            new TownNpcTag { Name = null!, TownCenterX = 0, TownCenterY = 0, WanderRadius = 1 }
        );

        var json = EntitySerializer.SerializeEntities(_engine.EcsWorld, 0, 0, Z);
        Assert.Contains("\"Name\":\"\"", json);

        // Round-trip should produce "NPC" as default name (from GetString default)
        // or empty string — either way it doesn't crash
        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(0, 0, Z);
        EntitySerializer.DeserializeEntities(json, engine2);
    }
}
