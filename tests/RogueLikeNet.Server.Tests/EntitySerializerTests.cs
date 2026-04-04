using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
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
        _engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Z));
    }

    public void Dispose() => _engine.Dispose();

    [Fact]
    public void SerializeEmpty_ReturnsEmptyArray()
    {
        // Chunk (99,99) has no entities - create an empty chunk
        var emptyChunk = new Chunk(ChunkPosition.FromCoords(99, 99, Z));
        var json = EntitySerializer.SerializeEntities(emptyChunk);
        Assert.Equal("[]", json);
    }

    [Fact]
    public void Monster_RoundTrip_PreservesData()
    {
        var md = new MonsterData { MonsterTypeId = 7, Health = 50, Attack = 12, Defense = 4, Speed = 8 };
        var monster = _engine.SpawnMonster(Position.FromCoords(1, 2, Z), md);

        // Tweak runtime state so we can verify it survives the round-trip
        ref var monsterRef = ref _engine.WorldMap.GetMonsterRef(monster.Id);
        monsterRef.Health.Current = 30;
        monsterRef.AI.StateId = 2;
        monsterRef.AI.PatrolX = 10;
        monsterRef.AI.PatrolY = 20;
        monsterRef.AI.AlertCooldown = 5;
        monsterRef.MoveDelay.Current = 3;
        monsterRef.AttackDelay.Current = 7;

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);
        Assert.Contains("\"Type\":\"Monster\"", json);

        // Deserialize into a fresh engine
        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Z));
        EntitySerializer.DeserializeEntities(json, engine2);

        // Find the deserialized monster
        var chunk2 = engine2.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var found = chunk2.Monsters.ToArray().FirstOrDefault(m => m.Position.X == 1 && m.Position.Y == 2 && m.Position.Z == Z);
        Assert.NotEqual(0, found.Id);

        Assert.Equal(7, found!.MonsterData.MonsterTypeId);
        Assert.Equal(50, found.MonsterData.Health);
        Assert.Equal(12, found.MonsterData.Attack);
        Assert.Equal(4, found.MonsterData.Defense);
        Assert.Equal(8, found.MonsterData.Speed);

        Assert.Equal(30, found.Health.Current);
        Assert.Equal(50, found.Health.Max);

        Assert.Equal(2, found.AI.StateId);
        Assert.Equal(10, found.AI.PatrolX);
        Assert.Equal(20, found.AI.PatrolY);
        Assert.Equal(5, found.AI.AlertCooldown);

        Assert.Equal(3, found.MoveDelay.Current);
        Assert.Equal(7, found.AttackDelay.Current);
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
        _engine.SpawnItemOnGround(itemData, Position.FromCoords(4, 5, Z));

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);
        Assert.Contains("\"Type\":\"GroundItem\"", json);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Z));
        EntitySerializer.DeserializeEntities(json, engine2);

        var chunk2 = engine2.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var found = chunk2.GroundItems.ToArray().FirstOrDefault(gi => gi.Position.X == 4 && gi.Position.Y == 5 && gi.Position.Z == Z);
        Assert.NotEqual(EntityRef.NullId, found.Id);

        Assert.Equal(3, found!.Item.ItemTypeId);
        Assert.Equal(2, found.Item.Rarity);
        Assert.Equal(5, found.Item.BonusAttack);
        Assert.Equal(3, found.Item.BonusDefense);
        Assert.Equal(10, found.Item.BonusHealth);
        Assert.Equal(4, found.Item.StackCount);
    }

    [Fact]
    public void ResourceNode_RoundTrip_PreservesData()
    {
        var def = ResourceNodeDefinitions.All[ResourceNodeDefinitions.CopperRock];
        var node = _engine.SpawnResourceNode(Position.FromCoords(3, 3, Z), def);

        // Damage the node to test HP preservation
        ref var nodeRef = ref _engine.WorldMap.GetResourceNodeRef(node.Id);
        nodeRef.Health.Current = 5;
        nodeRef.AttackDelay.Current = 2;

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);
        Assert.Contains("\"Type\":\"ResourceNode\"", json);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Z));
        EntitySerializer.DeserializeEntities(json, engine2);

        var chunk2 = engine2.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var found = chunk2.ResourceNodes.ToArray().FirstOrDefault(r => r.Position.X == 3 && r.Position.Y == 3 && r.Position.Z == Z);
        Assert.NotEqual(EntityRef.NullId, found.Id);

        Assert.Equal(ResourceNodeDefinitions.CopperRock, found!.NodeData.NodeTypeId);
        Assert.Equal(def.ResourceItemTypeId, found.NodeData.ResourceItemTypeId);
        Assert.Equal(def.MinDrop, found.NodeData.MinDrop);
        Assert.Equal(def.MaxDrop, found.NodeData.MaxDrop);

        Assert.Equal(5, found.Health.Current);
        Assert.Equal(def.Health, found.Health.Max);

        Assert.Equal(2, found.AttackDelay.Current);
    }

    [Fact]
    public void Element_RoundTrip_PreservesData()
    {
        var element = new DungeonElement(
            Position.FromCoords(6, 7, Z),
            new TileAppearance(99, 0xAABBCC, 0x112233),
            null
        );
        _engine.SpawnElement(element);

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);
        Assert.Contains("\"Type\":\"Element\"", json);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Z));
        EntitySerializer.DeserializeEntities(json, engine2);

        var chunk2 = engine2.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var found = chunk2.Elements.ToArray().FirstOrDefault(e => e.Position.X == 6 && e.Position.Y == 7 && e.Position.Z == Z);
        Assert.NotEqual(EntityRef.NullId, found.Id);

        Assert.Equal(99, found!.Appearance.GlyphId);
        Assert.Equal(0xAABBCC, found.Appearance.FgColor);
        Assert.Equal(0x112233, found.Appearance.BgColor);
    }

    [Fact]
    public void ElementWithLight_RoundTrip_PreservesData()
    {
        var element = new DungeonElement(
            Position.FromCoords(8, 9, Z),
            new TileAppearance(55, 0xFFCC66, 0x000000),
            new LightSource(5, 0xFFCC66)
        );
        _engine.SpawnElement(element);

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Z));
        EntitySerializer.DeserializeEntities(json, engine2);

        var chunk2 = engine2.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var found = chunk2.Elements.ToArray().FirstOrDefault(e => e.Position.X == 8 && e.Position.Y == 9 && e.Position.Z == Z);
        Assert.NotEqual(EntityRef.NullId, found.Id);

        Assert.NotNull(found!.Light);
        Assert.Equal(5, found.Light!.Value.Radius);
        Assert.Equal(0xFFCC66, found.Light.Value.ColorRgb);
    }

    [Fact]
    public void TownNpc_RoundTrip_PreservesData()
    {
        var npc = _engine.SpawnTownNpc(Position.FromCoords(2, 8, Z), "Blacksmith", 5, 10, 3);

        ref var npcRef = ref _engine.WorldMap.GetTownNpcRef(npc.Id);
        npcRef.Health.Current = 8000;
        npcRef.AI.StateId = 1;
        npcRef.MoveDelay.Current = 2;

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);
        Assert.Contains("\"Type\":\"TownNpc\"", json);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Z));
        EntitySerializer.DeserializeEntities(json, engine2);

        var chunk2 = engine2.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var found = chunk2.TownNpcs.ToArray().FirstOrDefault(n => n.Position.X == 2 && n.Position.Y == 8 && n.Position.Z == Z);
        Assert.NotEqual(EntityRef.NullId, found.Id);

        Assert.Equal("Blacksmith", found!.NpcData.Name);
        Assert.Equal(5, found.NpcData.TownCenterX);
        Assert.Equal(10, found.NpcData.TownCenterY);
        Assert.Equal(3, found.NpcData.WanderRadius);

        Assert.Equal(8000, found.Health.Current);
        Assert.Equal(9999, found.Health.Max);

        Assert.Equal(1, found.AI.StateId);
        Assert.Equal(5, found.MoveDelay.Interval);
        Assert.Equal(2, found.MoveDelay.Current);
    }

    [Fact]
    public void MultipleEntityTypes_RoundTrip()
    {
        _engine.SpawnMonster(Position.FromCoords(1, 1, Z), new MonsterData { MonsterTypeId = 1, Health = 10, Attack = 3, Defense = 1, Speed = 5 });
        _engine.SpawnItemOnGround(new ItemData { ItemTypeId = 2, StackCount = 1 }, Position.FromCoords(2, 2, Z));
        _engine.SpawnResourceNode(Position.FromCoords(3, 3, Z), ResourceNodeDefinitions.All[ResourceNodeDefinitions.CopperRock]);
        _engine.SpawnElement(new DungeonElement(
            Position.FromCoords(4, 4, Z),
            new TileAppearance(20, 0, 0),
            null
        ));
        _engine.SpawnElement(new DungeonElement(
            Position.FromCoords(5, 5, Z),
            new TileAppearance(30, 0, 0),
            new LightSource(3, 0xFFFFFF)
        ));
        _engine.SpawnTownNpc(Position.FromCoords(6, 6, Z), "Vendor", 6, 6, 2);

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Z));
        EntitySerializer.DeserializeEntities(json, engine2);

        var chunk2 = engine2.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        int monsters = chunk2.Monsters.ToArray().Count(m => !m.IsDead);
        int items = chunk2.GroundItems.ToArray().Count(gi => !gi.IsDestroyed);
        int nodes = chunk2.ResourceNodes.ToArray().Count(r => !r.IsDead);
        int elements = chunk2.Elements.Length;
        int npcs = chunk2.TownNpcs.ToArray().Count(n => !n.IsDead);

        Assert.True(monsters >= 1, $"Expected >= 1 monster, got {monsters}");
        Assert.True(items >= 1, $"Expected >= 1 item, got {items}");
        Assert.True(nodes >= 1, $"Expected >= 1 resource node, got {nodes}");
        Assert.True(elements >= 1, $"Expected >= 1 element, got {elements}");
        Assert.True(npcs >= 1, $"Expected >= 1 NPC, got {npcs}");
    }

    [Fact]
    public void Deserialize_EmptyString_DoesNothing()
    {
        EntitySerializer.DeserializeEntities("", _engine);
        EntitySerializer.DeserializeEntities("[]", _engine);
    }

    [Fact]
    public void Deserialize_NullString_DoesNothing()
    {
        EntitySerializer.DeserializeEntities(null!, _engine);
    }

    [Fact]
    public void OnlySerializesEntitiesInRequestedChunk()
    {
        // Spawn in chunk (0,0)
        _engine.SpawnMonster(Position.FromCoords(1, 1, Z), new MonsterData { MonsterTypeId = 1, Health = 10, Attack = 1, Defense = 1, Speed = 1 });

        // Serialize chunk (1,1) — should not contain the monster at (1,1) since that's in chunk (0,0)
        var chunk11 = new Chunk(ChunkPosition.FromCoords(1, 1, Z));
        var json = EntitySerializer.SerializeEntities(chunk11);

        Assert.DoesNotContain("\"Type\":\"Monster\"", json);
    }

    [Fact]
    public void Serialize_SkipsDeadEntities()
    {
        var monster = _engine.SpawnMonster(Position.FromCoords(1, 1, Z), new MonsterData { MonsterTypeId = 9999, Health = 10, Attack = 1, Defense = 1, Speed = 1 });

        ref var monsterRef = ref _engine.WorldMap.GetMonsterRef(monster.Id);
        monsterRef.Health.Current = 0;

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);
        Assert.DoesNotContain("\"MonsterTypeId\":9999", json);
    }

    [Fact]
    public void TownNpc_NullName_SerializesAsEmpty()
    {
        _engine.SpawnTownNpc(Position.FromCoords(2, 2, Z), null!, 0, 0, 1);

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);
        Assert.Contains("\"Name\":\"\"", json);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Z));
        EntitySerializer.DeserializeEntities(json, engine2);
    }

    /// <summary>
    /// Regression test: entities serialized into a saved chunk must survive the full
    /// PersistentDungeonGenerator roundtrip. Previously, DeserializeEntities ran before
    /// the chunk was registered in WorldMap, so Spawn* silently dropped all entities.
    /// </summary>
    [Fact]
    public void PersistentGenerator_RoundTrip_RestoresAllEntityTypes()
    {
        // 1. Create a world, spawn entities, serialize the chunk
        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        _engine.SpawnMonster(Position.FromCoords(1, 1, Z), new MonsterData { MonsterTypeId = 1, Health = 10, Attack = 3, Defense = 1, Speed = 5 });
        _engine.SpawnItemOnGround(new ItemData { ItemTypeId = 2, StackCount = 1 }, Position.FromCoords(2, 2, Z));
        _engine.SpawnResourceNode(Position.FromCoords(3, 3, Z), ResourceNodeDefinitions.All[ResourceNodeDefinitions.CopperRock]);
        _engine.SpawnTownNpc(Position.FromCoords(4, 4, Z), "Blacksmith", 4, 4, 3);

        var entityJson = EntitySerializer.SerializeEntities(chunk);
        var tileData = ChunkSerializer.SerializeTiles(chunk.Tiles);

        // 2. Set up an InMemorySaveGameProvider with the saved chunk
        var saveProvider = new InMemorySaveGameProvider();
        var slot = saveProvider.CreateSaveSlot("Test", 42, "bsp-dungeon");
        saveProvider.SaveChunks(slot.SlotId, [new ChunkSaveEntry
        {
            ChunkX = 0, ChunkY = 0, ChunkZ = Z,
            TileData = tileData,
            EntityData = entityJson,
        }]);

        // 3. Load via PersistentDungeonGenerator → GameEngine (the real pipeline)
        var persistentGen = new PersistentDungeonGenerator(_gen, saveProvider, slot.SlotId);
        using var engine2 = new GameEngine(42, persistentGen);
        engine2.RawEntityJsonHandler = EntitySerializer.DeserializeEntities;

        engine2.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Z));

        // 4. Verify all entity types were restored
        var loaded = engine2.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        Assert.True(loaded.Monsters.ToArray().Any(m => m.Position.X == 1 && m.Position.Y == 1), "Monster should be restored");
        Assert.True(loaded.GroundItems.ToArray().Any(gi => gi.Position.X == 2 && gi.Position.Y == 2), "Ground item should be restored");
        Assert.True(loaded.ResourceNodes.ToArray().Any(r => r.Position.X == 3 && r.Position.Y == 3), "Resource node should be restored");
        Assert.True(loaded.TownNpcs.ToArray().Any(n => n.Position.X == 4 && n.Position.Y == 4 && n.NpcData.Name == "Blacksmith"),
            "Town NPC should be restored");
    }
}
