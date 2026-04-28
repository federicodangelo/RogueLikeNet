using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
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
        var md = new MonsterData { MonsterTypeId = 7, Health = 50, Attack = 12, Defense = 4, Speed = 8, AttackSpeed = 2 };
        var monster = _engine.SpawnMonster(Position.FromCoords(1, 2, Z), md);

        // Tweak runtime state so we can verify it survives the round-trip
        ref var monsterRef = ref _engine.WorldMap.GetMonsterRef(monster.Id);
        monsterRef.Health.Current = 30;
        monsterRef.AI.StateId = 2;
        monsterRef.AI.PatrolX = 10;
        monsterRef.AI.PatrolY = 20;
        monsterRef.AI.AlertCooldown = 5;
        monsterRef.MoveDelay.Current = 1;
        monsterRef.AttackDelay.Current = 1;
        monsterRef.StatusEffects.AddOrRefresh(new StatusEffect
        {
            Type = StatusEffectType.Burning,
            DamageType = DamageType.Fire,
            DamagePerTick = 3,
            TickInterval = 20,
            TickCounter = 11,
            RemainingTicks = 40,
            SpeedMultiplierBase100 = 100,
            SourcePlayerEntityId = 99,
        });

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);
        Assert.Contains("\"Type\":\"Monster\"", json);
        Assert.Contains("\"StatusCount\":1", json);

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
        Assert.Equal(2, found.MonsterData.AttackSpeed);

        Assert.Equal(30, found.Health.Current);
        Assert.Equal(50, found.Health.Max);

        Assert.Equal(2, found.AI.StateId);
        Assert.Equal(10, found.AI.PatrolX);
        Assert.Equal(20, found.AI.PatrolY);
        Assert.Equal(5, found.AI.AlertCooldown);

        Assert.Equal(1, found.MoveDelay.Current);
        Assert.Equal(1, found.AttackDelay.Current);
        Assert.True(found.StatusEffects.HasEffect(StatusEffectType.Burning));
        var restoredEffect = found.StatusEffects.Get(0);
        Assert.Equal(DamageType.Fire, restoredEffect.DamageType);
        Assert.Equal(3, restoredEffect.DamagePerTick);
        Assert.Equal(11, restoredEffect.TickCounter);
        Assert.Equal(40, restoredEffect.RemainingTicks);
        Assert.Equal(99, restoredEffect.SourcePlayerEntityId);
    }

    [Fact]
    public void GroundItem_RoundTrip_PreservesData()
    {
        var itemData = new ItemData
        {
            ItemTypeId = 3,
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
        Assert.Equal(4, found.Item.StackCount);
    }

    [Fact]
    public void ResourceNode_RoundTrip_PreservesData()
    {
        var def = GameData.Instance.ResourceNodes.Get("copper_rock")!;
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

        Assert.Equal(def.NumericId, found!.NodeData.NodeTypeId);
        Assert.Equal(GameData.Instance.Items.GetNumericId(def.DropItemId), found.NodeData.ResourceItemTypeId);
        Assert.Equal(def.MinDrop, found.NodeData.MinDrop);
        Assert.Equal(def.MaxDrop, found.NodeData.MaxDrop);

        Assert.Equal(5, found.Health.Current);
        Assert.Equal(def.Health, found.Health.Max);

        Assert.Equal(2, found.AttackDelay.Current);
    }

    [Fact]
    public void TownNpc_RoundTrip_PreservesData()
    {
        var npc = _engine.SpawnTownNpc(Position.FromCoords(2, 8, Z), "Blacksmith", 5, 10, 3);
        int originalId = npc.Id;

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
        Assert.Equal(originalId, found.Id);

        Assert.Equal("Blacksmith", found!.NpcData.Name);
        Assert.Equal(5, found.NpcData.TownCenterX);
        Assert.Equal(10, found.NpcData.TownCenterY);
        Assert.Equal(3, found.NpcData.WanderRadius);

        Assert.Equal(8000, found.Health.Current);
        Assert.Equal(9999, found.Health.Max);

        Assert.Equal(1, found.AI.StateId);
        Assert.Equal(20, found.MoveDelay.Interval);
        Assert.Equal(2, found.MoveDelay.Current);
    }

    [Fact]
    public void MultipleEntityTypes_RoundTrip()
    {
        _engine.SpawnMonster(Position.FromCoords(1, 1, Z), new MonsterData { MonsterTypeId = 1, Health = 10, Attack = 3, Defense = 1, Speed = 5, AttackSpeed = 1 });
        _engine.SpawnItemOnGround(new ItemData { ItemTypeId = 2, StackCount = 1 }, Position.FromCoords(2, 2, Z));
        _engine.SpawnResourceNode(Position.FromCoords(3, 3, Z), GameData.Instance.ResourceNodes.Get("copper_rock")!);
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
        int npcs = chunk2.TownNpcs.ToArray().Count(n => !n.IsDead);

        Assert.True(monsters >= 1, $"Expected >= 1 monster, got {monsters}");
        Assert.True(items >= 1, $"Expected >= 1 item, got {items}");
        Assert.True(nodes >= 1, $"Expected >= 1 resource node, got {nodes}");
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
        _engine.SpawnMonster(Position.FromCoords(1, 1, Z), new MonsterData { MonsterTypeId = 1, Health = 10, Attack = 1, Defense = 1, Speed = 1, AttackSpeed = 0 });

        // Serialize chunk (1,1) — should not contain the monster at (1,1) since that's in chunk (0,0)
        var chunk11 = new Chunk(ChunkPosition.FromCoords(1, 1, Z));
        var json = EntitySerializer.SerializeEntities(chunk11);

        Assert.DoesNotContain("\"Type\":\"Monster\"", json);
    }

    [Fact]
    public void Serialize_SkipsDeadEntities()
    {
        var monster = _engine.SpawnMonster(Position.FromCoords(1, 1, Z), new MonsterData { MonsterTypeId = 9999, Health = 10, Attack = 1, Defense = 1, Speed = 1, AttackSpeed = 0 });

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
        _engine.SpawnMonster(Position.FromCoords(1, 1, Z), new MonsterData { MonsterTypeId = 1, Health = 10, Attack = 3, Defense = 1, Speed = 5, AttackSpeed = 1 });
        _engine.SpawnItemOnGround(new ItemData { ItemTypeId = 2, StackCount = 1 }, Position.FromCoords(2, 2, Z));
        _engine.SpawnResourceNode(Position.FromCoords(3, 3, Z), GameData.Instance.ResourceNodes.Get("copper_rock")!);
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

    [Fact]
    public void Crop_RoundTrip_PreservesData()
    {
        // Find a seed item
        var seedDef = GameData.Instance.Items.All.FirstOrDefault(i => i.Seed != null);
        if (seedDef == null) return; // skip if no seed items loaded

        var crop = _engine.SpawnCrop(Position.FromCoords(5, 5, Z), seedDef);
        ref var cropRef = ref _engine.WorldMap.GetChunk(ChunkPosition.FromCoords(0, 0, Z)).GetCropRef(crop.Id);
        cropRef.CropData.GrowthTicksCurrent = 42;
        cropRef.CropData.IsWatered = true;

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);
        Assert.Contains("\"Type\":\"Crop\"", json);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Z));
        EntitySerializer.DeserializeEntities(json, engine2);

        var chunk2 = engine2.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var found = chunk2.Crops.ToArray().FirstOrDefault(c => c.Position.X == 5 && c.Position.Y == 5 && c.Position.Z == Z);
        Assert.NotEqual(EntityRef.NullId, found.Id);

        Assert.Equal(seedDef.NumericId, found!.CropData.SeedItemTypeId);
        Assert.Equal(42, found.CropData.GrowthTicksCurrent);
        Assert.True(found.CropData.IsWatered);
    }

    [Fact]
    public void Animal_RoundTrip_PreservesData()
    {
        var animalDef = GameData.Instance.Animals.All.FirstOrDefault();
        if (animalDef == null) return; // skip if no animal definitions

        var animal = _engine.SpawnAnimal(Position.FromCoords(7, 7, Z), animalDef);
        ref var animalRef = ref _engine.WorldMap.GetChunk(ChunkPosition.FromCoords(0, 0, Z)).GetAnimalRef(animal.Id);
        animalRef.Health.Current = 5;
        animalRef.AnimalData.ProduceTicksCurrent = 100;
        animalRef.AnimalData.IsFed = true;
        animalRef.AnimalData.FedTicksRemaining = 50;
        animalRef.AnimalData.BreedCooldownCurrent = 30;
        animalRef.AI.StateId = 1;
        animalRef.MoveDelay.Current = 3;

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);
        Assert.Contains("\"Type\":\"Animal\"", json);

        using var engine2 = new GameEngine(42, _gen);
        engine2.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Z));
        EntitySerializer.DeserializeEntities(json, engine2);

        var chunk2 = engine2.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var found = chunk2.Animals.ToArray().FirstOrDefault(a => a.Position.X == 7 && a.Position.Y == 7 && a.Position.Z == Z);
        Assert.NotEqual(EntityRef.NullId, found.Id);

        Assert.Equal(animalDef.NumericId, found!.AnimalData.AnimalTypeId);
        Assert.Equal(5, found.Health.Current);
        Assert.Equal(100, found.AnimalData.ProduceTicksCurrent);
        Assert.True(found.AnimalData.IsFed);
        Assert.Equal(50, found.AnimalData.FedTicksRemaining);
        Assert.Equal(30, found.AnimalData.BreedCooldownCurrent);
        Assert.Equal(1, found.AI.StateId);
        Assert.Equal(3, found.MoveDelay.Current);
    }

    [Fact]
    public void Serialize_SkipsDestroyedItems()
    {
        var item = _engine.SpawnItemOnGround(new ItemData { ItemTypeId = 8888, StackCount = 1 }, Position.FromCoords(9, 9, Z));
        ref var itemRef = ref _engine.WorldMap.GetGroundItemRef(item.Id);
        itemRef.IsDestroyed = true;

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);
        Assert.DoesNotContain("\"ItemTypeId\":8888", json);
    }

    [Fact]
    public void Serialize_SkipsDestroyedCrops()
    {
        var seedDef = GameData.Instance.Items.All.FirstOrDefault(i => i.Seed != null);
        if (seedDef == null) return;

        var crop = _engine.SpawnCrop(Position.FromCoords(10, 10, Z), seedDef);
        ref var cropRef = ref _engine.WorldMap.GetChunk(ChunkPosition.FromCoords(0, 0, Z)).GetCropRef(crop.Id);
        cropRef.IsDestroyed = true;

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);
        // Should not contain a Crop entry with our specific position
        Assert.DoesNotContain("\"GrowthTicksCurrent\":0", json.Substring(json.LastIndexOf("\"Type\":\"Crop\"") >= 0 ? json.LastIndexOf("\"Type\":\"Crop\"") : 0));
    }

    [Fact]
    public void Serialize_SkipsDeadAnimals()
    {
        var animalDef = GameData.Instance.Animals.All.FirstOrDefault();
        if (animalDef == null) return;

        var animal = _engine.SpawnAnimal(Position.FromCoords(11, 11, Z), animalDef);
        ref var animalRef = ref _engine.WorldMap.GetChunk(ChunkPosition.FromCoords(0, 0, Z)).GetAnimalRef(animal.Id);
        animalRef.Health.Current = 0;

        var chunk = _engine.WorldMap.TryGetChunk(ChunkPosition.FromCoords(0, 0, Z))!;
        var json = EntitySerializer.SerializeEntities(chunk);
        // The dead animal should not be serialized
        var animalEntries = json.Split("\"Type\":\"Animal\"").Length - 1;
        Assert.Equal(0, animalEntries);
    }
}
