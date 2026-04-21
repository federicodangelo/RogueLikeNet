using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Protocol.Tests;

public class GameStateSerializerTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    [Fact]
    public void EntityUpdateMsg_SameValues_DetectsEquality()
    {
        var a = new EntityUpdateMsg { Id = 1, X = 2, Y = 3, GlyphId = 64, FgColor = 0xFFFFFF, Health = 10, MaxHealth = 10 };
        var b = new EntityUpdateMsg { Id = 1, X = 2, Y = 3, GlyphId = 64, FgColor = 0xFFFFFF, Health = 10, MaxHealth = 10 };
        var c = new EntityUpdateMsg { Id = 1, X = 2, Y = 4, GlyphId = 64, FgColor = 0xFFFFFF, Health = 10, MaxHealth = 10 };

        Assert.True(a.SameValues(b));
        Assert.False(a.SameValues(c));
    }

    [Fact]
    public void EntityUpdateMsg_HasOnlyPositionChanges_DetectsDiff()
    {
        var prev = new EntityUpdateMsg { Id = 1, X = 2, Y = 3, GlyphId = 64, FgColor = 0xFFFFFF, Health = 10, MaxHealth = 10 };
        var posOnly = new EntityUpdateMsg { Id = 1, X = 5, Y = 6, GlyphId = 64, FgColor = 0xFFFFFF, Health = 8, MaxHealth = 10 };
        var full = new EntityUpdateMsg { Id = 1, X = 5, Y = 6, GlyphId = 65, FgColor = 0xFFFFFF, Health = 8, MaxHealth = 10 };

        Assert.True(posOnly.HasOnlyPositionHealthChanges(prev));
        Assert.False(full.HasOnlyPositionHealthChanges(prev));
    }

    [Fact]
    public void SerializeEntityDelta_OnlyReturnsChangedEntities()
    {
        using var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var monster = engine.SpawnMonster(Position.FromCoords(sx, sy, Position.DefaultZ), new MonsterData
        {
            MonsterTypeId = 1,
            Health = 10,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        var fov = new FOVData(10);
        fov.VisibleTiles!.Add(Position.PackCoord(sx, sy, Position.DefaultZ));

        var previousState = new Dictionary<long, EntityUpdateMsg>();

        // First call — entity is new, should be included as full update
        var (full1, pos1, rem1) = GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov, previousState);
        Assert.True(full1.Length > 0);
        Assert.Empty(pos1);
        Assert.Empty(rem1);

        // Second call — nothing changed, should NOT be included
        var (full2, pos2, rem2) = GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov, previousState);
        Assert.Empty(full2);
        Assert.Empty(pos2);
        Assert.Empty(rem2);
    }

    [Fact]
    public void SerializeEntityDelta_PositionOnlyUpdate()
    {
        using var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var monster = engine.SpawnMonster(Position.FromCoords(sx, sy, Position.DefaultZ), new MonsterData
        {
            MonsterTypeId = 1,
            Health = 10,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        var fov = new FOVData(10);
        fov.VisibleTiles!.Add(Position.PackCoord(sx, sy, Position.DefaultZ));
        fov.VisibleTiles!.Add(Position.PackCoord(sx + 1, sy, Position.DefaultZ));

        var previousState = new Dictionary<long, EntityUpdateMsg>();

        // Seed previous state
        GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov, previousState);

        // Move entity — only position changed
        ref var monsterRef = ref engine.WorldMap.GetMonsterRef(monster.Id);
        monsterRef.Position = Position.FromCoords(sx + 1, sy, Position.DefaultZ);

        var (full, pos, rem) = GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov, previousState);
        Assert.Empty(full);
        Assert.Single(pos);
        Assert.Equal(sx + 1, pos[0].X);
        Assert.Empty(rem);
    }

    [Fact]
    public void SerializeEntityDelta_RemovedEntitiesLeavingFOV()
    {
        using var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var monster = engine.SpawnMonster(Position.FromCoords(sx, sy, Position.DefaultZ), new MonsterData
        {
            MonsterTypeId = 1,
            Health = 10,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        var fovAll = new FOVData(10);
        fovAll.VisibleTiles!.Add(Position.PackCoord(sx, sy, Position.DefaultZ));

        var previousState = new Dictionary<long, EntityUpdateMsg>();

        // Seed previous state — entity visible
        GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov: fovAll, previousState);

        // Now entity moves out of FOV (or FOV shrinks)
        var fovNone = new FOVData(10);
        // No visible tiles

        var (full, pos, rem) = GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fovNone, previousState);
        Assert.Empty(full);
        Assert.Empty(pos);
        Assert.True(rem.Length > 0);
        Assert.Empty(previousState); // Cleaned up
    }

    [Fact]
    public void ChatMsg_RoundTrip()
    {
        var msg = new ChatMsg
        {
            SenderId = 42,
            SenderName = "Player1",
            Text = "Hello world!",
            Timestamp = 1234567890
        };

        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<ChatMsg>(data);

        Assert.Equal(42, result.SenderId);
        Assert.Equal("Player1", result.SenderName);
        Assert.Equal("Hello world!", result.Text);
        Assert.Equal(1234567890, result.Timestamp);
    }

    [Fact]
    public void FullSnapshot_SerializeDeserialize_RoundTrip()
    {
        using var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        engine.Tick();

        var previousState = new Dictionary<long, EntityUpdateMsg>();
        var (fullUpdates, posUpdates, removals) = GameStateSerializer.SerializeEntityDelta(engine.WorldMap, player.FOV, previousState);
        var snapshot = new WorldDeltaMsg
        {
            FromTick = 0,
            ToTick = engine.CurrentTick,
            IsSnapshot = true,
            Chunks = GameStateSerializer.SerializeChunksAroundPosition(engine, player.Position, -1),
            EntityUpdates = fullUpdates,
            EntityPositionHealthUpdates = posUpdates,
            EntityRemovals = removals,
            PlayerState = GameStateSerializer.BuildPlayerState(engine, player),
        };

        // Serialize + wrap (server side)
        var payload = NetSerializer.Serialize(snapshot);
        var data = NetSerializer.WrapMessage(MessageTypes.WorldDelta, payload);

        // Unwrap + deserialize (client side)
        var envelope = NetSerializer.UnwrapMessage(data);
        Assert.Equal(MessageTypes.WorldDelta, envelope.MessageType);

        var result = NetSerializer.Deserialize<WorldDeltaMsg>(envelope.Payload);
        Assert.True(result.IsSnapshot);
        Assert.Equal(snapshot.ToTick, result.ToTick);
        Assert.Equal(9, result.Chunks.Length); // 3x3 around player
        Assert.True(result.EntityUpdates.Length > 0);
        Assert.NotNull(result.PlayerState);
    }

    [Fact]
    public void SerializeEntityDelta_PositionHealthOnlyChange_EmitsPositionHealthUpdate()
    {
        using var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var monster = engine.SpawnMonster(Position.FromCoords(sx, sy, Position.DefaultZ), new MonsterData
        {
            MonsterTypeId = 1,
            Health = 20,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        var fov = new FOVData(10);
        fov.VisibleTiles!.Add(Position.PackCoord(sx, sy, Position.DefaultZ));
        fov.VisibleTiles!.Add(Position.PackCoord(sx + 2, sy, Position.DefaultZ));

        var previousState = new Dictionary<long, EntityUpdateMsg>();

        // Seed previous state
        GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov, previousState);

        // Change only position and health — glyph and color stay the same
        ref var monsterRef = ref engine.WorldMap.GetMonsterRef(monster.Id);
        monsterRef.Position = Position.FromCoords(sx + 2, sy, Position.DefaultZ);
        monsterRef.Health.Current = 15;

        var (full, pos, rem) = GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov, previousState);
        Assert.Empty(full); // Not a full update
        Assert.Single(pos); // Should be a position-health update
        Assert.Equal(sx + 2, pos[0].X);
        Assert.Equal(sy, pos[0].Y);
        Assert.Equal(15, pos[0].Health);
        Assert.Empty(rem);
    }

    // ── SerializePlayerStateDelta ────────────────────────────────────────────

    private static (RogueLikeNet.Core.GameEngine engine, PlayerEntity player) CreateEngineWithPlayer()
    {
        var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        return (engine, player);
    }

    [Fact]
    public void SerializePlayerStateDelta_ReturnsStateOnFirstCall()
    {
        var (engine, player) = CreateEngineWithPlayer();

        var result = GameStateSerializer.SerializePlayerStateDelta(engine, player, null);

        Assert.NotNull(result);
        Assert.Equal(player.Id, result.PlayerEntityId);

        engine.Dispose();
    }

    [Fact]
    public void SerializePlayerStateDelta_ReturnsNullWhenStateUnchanged()
    {
        var (engine, player) = CreateEngineWithPlayer();

        var first = GameStateSerializer.SerializePlayerStateDelta(engine, player, null);
        Assert.NotNull(first);

        var second = GameStateSerializer.SerializePlayerStateDelta(engine, player, first);
        Assert.Null(second);

        engine.Dispose();
    }

    [Fact]
    public void SerializePlayerStateDelta_DetectsHealthChange()
    {
        var (engine, player) = CreateEngineWithPlayer();

        var first = GameStateSerializer.SerializePlayerStateDelta(engine, player, null);
        Assert.NotNull(first);

        player.Health.Current -= 5;

        var second = GameStateSerializer.SerializePlayerStateDelta(engine, player, first);
        Assert.NotNull(second);
        Assert.Equal(player.Health.Current, second.Health);

        engine.Dispose();
    }

    [Fact]
    public void SerializePlayerStateDelta_DetectsInventoryChange()
    {
        var (engine, player) = CreateEngineWithPlayer();

        player.Inventory.Items.Add(new ItemData { ItemTypeId = GameData.Instance.Items.GetNumericId("health_potion_small"), StackCount = 1 });

        var first = GameStateSerializer.SerializePlayerStateDelta(engine, player, null);
        Assert.NotNull(first);
        int initialCount = first.InventoryCount;

        player.Inventory.Items.Add(new ItemData { ItemTypeId = GameData.Instance.Items.GetNumericId("health_potion_small"), StackCount = 1 });

        var second = GameStateSerializer.SerializePlayerStateDelta(engine, player, first);
        Assert.NotNull(second);
        Assert.Equal(initialCount + 1, second.InventoryCount);
        Assert.True(second.InventoryItems.Length > 0);
        Assert.Equal(GameData.Instance.Items.GetNumericId("health_potion_small"), second.InventoryItems[^1].ItemTypeId);

        engine.Dispose();
    }

    [Fact]
    public void SerializePlayerStateDelta_DetectsEquippedWeaponChange()
    {
        var (engine, player) = CreateEngineWithPlayer();

        var first = GameStateSerializer.SerializePlayerStateDelta(engine, player, null);
        Assert.NotNull(first);
        Assert.Empty(first.EquippedItems);

        player.Equipment.Hand = new ItemData { ItemTypeId = GameData.Instance.Items.GetNumericId("short_sword"), StackCount = 1 };

        var second = GameStateSerializer.SerializePlayerStateDelta(engine, player, first);
        Assert.NotNull(second);
        Assert.Single(second.EquippedItems);
        Assert.Equal(GameData.Instance.Items.GetNumericId("short_sword"), second.EquippedItems[0].ItemTypeId);

        engine.Dispose();
    }

    [Fact]
    public void SerializePlayerStateDelta_DetectsEquippedArmorChange()
    {
        var (engine, player) = CreateEngineWithPlayer();

        var first = GameStateSerializer.SerializePlayerStateDelta(engine, player, null);
        Assert.NotNull(first);
        Assert.Empty(first.EquippedItems);

        player.Equipment.Chest = new ItemData { ItemTypeId = GameData.Instance.Items.GetNumericId("leather_armor"), StackCount = 1 };

        var second = GameStateSerializer.SerializePlayerStateDelta(engine, player, first);
        Assert.NotNull(second);
        Assert.Single(second.EquippedItems);
        Assert.Equal(GameData.Instance.Items.GetNumericId("leather_armor"), second.EquippedItems[0].ItemTypeId);

        engine.Dispose();
    }

    [Fact]
    public void SerializePlayerStateDelta_DetectsQuickSlotChange()
    {
        var (engine, player) = CreateEngineWithPlayer();

        player.Inventory.Items.Add(new ItemData { ItemTypeId = GameData.Instance.Items.GetNumericId("health_potion_small"), StackCount = 1 });

        var first = GameStateSerializer.SerializePlayerStateDelta(engine, player, null);
        Assert.NotNull(first);
        Assert.Equal(-1, first.QuickSlotIndices[0]); // default: slot empty

        player.QuickSlots.Slot0 = 0;

        var second = GameStateSerializer.SerializePlayerStateDelta(engine, player, first);
        Assert.NotNull(second);
        Assert.Equal(0, second.QuickSlotIndices[0]);

        engine.Dispose();
    }

    // ── SerializeCombatEvents ──

    [Fact]
    public void SerializeCombatEvents_EmptyWhenNoCombat()
    {
        using var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        var events = GameStateSerializer.SerializeCombatEvents(engine);
        Assert.Empty(events);
    }

    // ── SerializeNpcInteractions ──

    [Fact]
    public void SerializeNpcInteractions_EmptyWhenNoEvents()
    {
        var (engine, player) = CreateEngineWithPlayer();
        var events = GameStateSerializer.SerializeNpcInteractions(engine, player);
        Assert.Empty(events);
        engine.Dispose();
    }

    // ── SerializeChunk ──

    [Fact]
    public void SerializeChunk_ReturnsCorrectDimensions()
    {
        using var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        var chunk = engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        var msg = GameStateSerializer.SerializeChunk(chunk, -1);

        Assert.Equal(0, msg.ChunkX);
        Assert.Equal(0, msg.ChunkY);
        Assert.Equal(Position.DefaultZ, msg.ChunkZ);
        int total = Chunk.Size * Chunk.Size;
        Assert.Equal(total, msg.TileIds.Length);
        Assert.Equal(total, msg.TilePlaceableItemIds.Length);
        Assert.Equal(total, msg.TilePlaceableItemExtras.Length);
    }

    // ── SerializeChunksAroundPosition ──

    [Fact]
    public void SerializeChunksAroundPosition_Returns9Chunks()
    {
        using var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var pos = Position.FromCoords(16, 16, Position.DefaultZ);
        var chunks = GameStateSerializer.SerializeChunksAroundPosition(engine, pos, -1);
        Assert.Equal(9, chunks.Length);
    }

    // ── SerializeChunksDelta ──

    [Fact]
    public void SerializeChunksDelta_NewChunksIncluded()
    {
        using var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        var tracker = new ChunkTracker();
        var pos = Position.FromCoords(16, 16, Position.DefaultZ);

        var result = GameStateSerializer.SerializeChunksDelta(engine, pos, tracker, 50, -1);
        Assert.True(result.NewChunks.Length > 0);
    }

    [Fact]
    public void SerializeChunksDelta_SecondCall_NoNewChunks()
    {
        using var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        var tracker = new ChunkTracker();
        var pos = Position.FromCoords(16, 16, Position.DefaultZ);

        GameStateSerializer.SerializeChunksDelta(engine, pos, tracker, 50, -1);
        var result2 = GameStateSerializer.SerializeChunksDelta(engine, pos, tracker, 50, -1);
        Assert.Empty(result2.NewChunks);
    }

    // ── SerializeEntityDelta with debugVisibilityOff ──

    [Fact]
    public void SerializeEntityDelta_DebugVisibilityOff_IncludesAllEntities()
    {
        using var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        engine.SpawnMonster(Position.FromCoords(sx, sy, Position.DefaultZ), new MonsterData
        {
            MonsterTypeId = 1,
            Health = 10,
            Attack = 5,
            Defense = 0,
            Speed = 8
        });

        // FOV has NO visible tiles, but debugVisibilityOff = true should include everything
        var fov = new FOVData(10);
        var previousState = new Dictionary<long, EntityUpdateMsg>();
        var (full, _, _) = GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov, previousState, debugVisibilityOff: true);
        Assert.True(full.Length > 0);
    }

    // ── SerializeEntityDelta: various entity types ──

    [Fact]
    public void SerializeEntityDelta_IncludesGroundItems()
    {
        using var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var pos = Position.FromCoords(sx, sy, Position.DefaultZ);
        engine.SpawnItemOnGround(new ItemData { ItemTypeId = 1, StackCount = 1 }, pos);

        var fov = new FOVData(10);
        fov.VisibleTiles!.Add(Position.PackCoord(pos));
        var previousState = new Dictionary<long, EntityUpdateMsg>();

        var (full, _, _) = GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov, previousState);
        Assert.Contains(full, e => e.EntityType == (int)EntityType.GroundItem);
    }

    [Fact]
    public void SerializeEntityDelta_IncludesResourceNodes()
    {
        using var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var pos = Position.FromCoords(sx, sy, Position.DefaultZ);
        var def = GameData.Instance.ResourceNodes.All.First();
        engine.SpawnResourceNode(pos, def);

        var fov = new FOVData(10);
        fov.VisibleTiles!.Add(Position.PackCoord(pos));
        var previousState = new Dictionary<long, EntityUpdateMsg>();

        var (full, _, _) = GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov, previousState);
        Assert.Contains(full, e => e.EntityType == (int)EntityType.ResourceNode);
    }

    [Fact]
    public void SerializeEntityDelta_IncludesPlayers()
    {
        using var engine = new RogueLikeNet.Core.GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);

        var fov = new FOVData(10);
        fov.VisibleTiles!.Add(Position.PackCoord(player.Position));
        var previousState = new Dictionary<long, EntityUpdateMsg>();

        var (full, _, _) = GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov, previousState);
        Assert.Contains(full, e => e.EntityType == (int)EntityType.Player);
    }

    // ── BuildPlayerState ──

    [Fact]
    public void BuildPlayerState_ReturnsCorrectData()
    {
        var (engine, player) = CreateEngineWithPlayer();

        var state = GameStateSerializer.BuildPlayerState(engine, player);

        Assert.NotNull(state);
        Assert.Equal(player.Health.Current, state.Health);
        Assert.Equal(player.Health.Max, state.MaxHealth);
        Assert.Equal((long)player.Id, state.PlayerEntityId);

        engine.Dispose();
    }

    // ── Non-empty combat events ──

    [Fact]
    public void SerializeCombatEvents_WithCombat_ReturnsEvents()
    {
        var (engine, player) = CreateEngineWithPlayer();
        ref var p = ref engine.WorldMap.GetPlayerRef(player.Id);

        // Spawn monster adjacent and attack it
        engine.SpawnMonster(Position.FromCoords(p.Position.X + 1, p.Position.Y, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        p.Input.ActionType = ActionTypes.Attack;
        p.Input.TargetX = 1;
        p.Input.TargetY = 0;
        engine.Tick();

        var events = GameStateSerializer.SerializeCombatEvents(engine);
        Assert.NotEmpty(events);
        Assert.True(events[0].Damage > 0);

        engine.Dispose();
    }

    // ── Non-empty dialogue events ──

    [Fact]
    public void SerializeNpcDialogueEvents_WithDialogue_ReturnsEvents()
    {
        var (engine, player) = CreateEngineWithPlayer();
        ref var p = ref engine.WorldMap.GetPlayerRef(player.Id);

        // Spawn town NPC adjacent and attack it
        engine.SpawnTownNpc(
            Position.FromCoords(p.Position.X + 1, p.Position.Y, Position.DefaultZ),
            "TestNpc", p.Position.X, p.Position.Y, 5);
        p.Input.ActionType = ActionTypes.Attack;
        p.Input.TargetX = 1;
        p.Input.TargetY = 0;
        engine.Tick();

        var events = GameStateSerializer.SerializeNpcInteractions(engine, p);
        Assert.NotEmpty(events);
        Assert.Equal("TestNpc", events[0].NpcName);

        engine.Dispose();
    }

    // ── Entity delta with TownNpcs, Crops, Animals ──

    [Fact]
    public void SerializeEntityDelta_IncludesTownNpcs()
    {
        var (engine, player) = CreateEngineWithPlayer();
        ref var p = ref engine.WorldMap.GetPlayerRef(player.Id);

        engine.SpawnTownNpc(
            Position.FromCoords(p.Position.X + 1, p.Position.Y, Position.DefaultZ),
            "NpcTest", p.Position.X, p.Position.Y, 5);

        var fov = new FOVData();
        var prevState = new Dictionary<long, EntityUpdateMsg>();
        var result = GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov, prevState, debugVisibilityOff: true);

        Assert.Contains(result.FullUpdates, e => e.EntityType == (int)EntityType.TownNpc);
        engine.Dispose();
    }

    [Fact]
    public void SerializeEntityDelta_IncludesCrops()
    {
        var (engine, player) = CreateEngineWithPlayer();
        ref var p = ref engine.WorldMap.GetPlayerRef(player.Id);

        var seedDef = GameData.Instance.Items.Get("wheat_seeds");
        if (seedDef != null)
        {
            engine.SpawnCrop(Position.FromCoords(p.Position.X + 1, p.Position.Y, Position.DefaultZ), seedDef);

            var fov = new FOVData();
            var prevState = new Dictionary<long, EntityUpdateMsg>();
            var result = GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov, prevState, debugVisibilityOff: true);

            Assert.Contains(result.FullUpdates, e => e.EntityType == (int)EntityType.Crop);
        }
        engine.Dispose();
    }

    [Fact]
    public void SerializeEntityDelta_IncludesAnimals()
    {
        var (engine, player) = CreateEngineWithPlayer();
        ref var p = ref engine.WorldMap.GetPlayerRef(player.Id);

        var animalDef = GameData.Instance.Animals.All.FirstOrDefault();
        if (animalDef != null)
        {
            engine.SpawnAnimal(Position.FromCoords(p.Position.X + 1, p.Position.Y, Position.DefaultZ), animalDef);

            var fov = new FOVData();
            var prevState = new Dictionary<long, EntityUpdateMsg>();
            var result = GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov, prevState, debugVisibilityOff: true);

            Assert.Contains(result.FullUpdates, e => e.EntityType == (int)EntityType.Animal);
        }
        engine.Dispose();
    }

    [Fact]
    public void SerializeEntityDelta_DeadEntities_Excluded()
    {
        var (engine, player) = CreateEngineWithPlayer();
        ref var p = ref engine.WorldMap.GetPlayerRef(player.Id);

        var m = engine.SpawnMonster(Position.FromCoords(p.Position.X + 1, p.Position.Y, Position.DefaultZ),
            new MonsterData { MonsterTypeId = 0, Health = 100, Attack = 5, Defense = 0, Speed = 8 });
        ref var monster = ref engine.WorldMap.GetMonsterRef(m.Id);
        monster.Health.Current = 0; // Kill it

        var fov = new FOVData();
        var prevState = new Dictionary<long, EntityUpdateMsg>();
        var result = GameStateSerializer.SerializeEntityDelta(engine.WorldMap, fov, prevState, debugVisibilityOff: true);

        Assert.DoesNotContain(result.FullUpdates, e => e.Id == (long)m.Id);
        engine.Dispose();
    }

    [Fact]
    public void SerializeChunksDelta_MaxChunksLimit_RespectsLimit()
    {
        var (engine, player) = CreateEngineWithPlayer();
        var tracker = new ChunkTracker();

        var result = GameStateSerializer.SerializeChunksDelta(engine, player.Position, tracker, visibleChunks: 9, maxChunksToSerialize: 2, serverPlayerId: -1);

        Assert.True(result.NewChunks.Length <= 2);
        engine.Dispose();
    }

    [Fact]
    public void SerializePlayerStateDelta_NullLastState_ReturnsState()
    {
        var (engine, player) = CreateEngineWithPlayer();

        var result = GameStateSerializer.SerializePlayerStateDelta(engine, player, null);
        Assert.NotNull(result);

        engine.Dispose();
    }

    [Fact]
    public void SerializePlayerStateDelta_SameState_ReturnsNull()
    {
        var (engine, player) = CreateEngineWithPlayer();

        var first = GameStateSerializer.SerializePlayerStateDelta(engine, player, null);
        Assert.NotNull(first);

        var second = GameStateSerializer.SerializePlayerStateDelta(engine, player, first);
        Assert.Null(second);

        engine.Dispose();
    }
}
