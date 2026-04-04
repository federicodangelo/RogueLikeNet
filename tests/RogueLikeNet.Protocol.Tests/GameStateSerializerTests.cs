using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
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
            Chunks = GameStateSerializer.SerializeChunksAroundPosition(engine, player.Position),
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

        var first = GameStateSerializer.SerializePlayerStateDelta(engine, player, null);
        Assert.NotNull(first);
        int initialCount = first.InventoryCount;

        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemDefinitions.HealthPotion, StackCount = 1, Rarity = ItemDefinitions.RarityCommon });

        var second = GameStateSerializer.SerializePlayerStateDelta(engine, player, first);
        Assert.NotNull(second);
        Assert.Equal(initialCount + 1, second.InventoryCount);
        Assert.Single(second.InventoryItems);
        Assert.Equal(ItemDefinitions.HealthPotion, second.InventoryItems[0].ItemTypeId);

        engine.Dispose();
    }

    [Fact]
    public void SerializePlayerStateDelta_DetectsEquippedWeaponChange()
    {
        var (engine, player) = CreateEngineWithPlayer();

        var first = GameStateSerializer.SerializePlayerStateDelta(engine, player, null);
        Assert.NotNull(first);
        Assert.Null(first.EquippedWeapon);

        player.Equipment.Weapon = new ItemData { ItemTypeId = ItemDefinitions.ShortSword, StackCount = 1, Rarity = ItemDefinitions.RarityCommon, BonusAttack = 3 };

        var second = GameStateSerializer.SerializePlayerStateDelta(engine, player, first);
        Assert.NotNull(second);
        Assert.NotNull(second.EquippedWeapon);
        Assert.Equal(ItemDefinitions.ShortSword, second.EquippedWeapon.ItemTypeId);

        engine.Dispose();
    }

    [Fact]
    public void SerializePlayerStateDelta_DetectsEquippedArmorChange()
    {
        var (engine, player) = CreateEngineWithPlayer();

        var first = GameStateSerializer.SerializePlayerStateDelta(engine, player, null);
        Assert.NotNull(first);
        Assert.Null(first.EquippedArmor);

        player.Equipment.Armor = new ItemData { ItemTypeId = ItemDefinitions.LeatherArmor, StackCount = 1, Rarity = ItemDefinitions.RarityCommon, BonusDefense = 2 };

        var second = GameStateSerializer.SerializePlayerStateDelta(engine, player, first);
        Assert.NotNull(second);
        Assert.NotNull(second.EquippedArmor);
        Assert.Equal(ItemDefinitions.LeatherArmor, second.EquippedArmor.ItemTypeId);

        engine.Dispose();
    }

    [Fact]
    public void SerializePlayerStateDelta_DetectsQuickSlotChange()
    {
        var (engine, player) = CreateEngineWithPlayer();

        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemDefinitions.HealthPotion, StackCount = 1, Rarity = ItemDefinitions.RarityCommon });

        var first = GameStateSerializer.SerializePlayerStateDelta(engine, player, null);
        Assert.NotNull(first);
        Assert.Equal(-1, first.QuickSlotIndices[0]); // default: slot empty

        player.QuickSlots.Slot0 = 0;

        var second = GameStateSerializer.SerializePlayerStateDelta(engine, player, first);
        Assert.NotNull(second);
        Assert.Equal(0, second.QuickSlotIndices[0]);

        engine.Dispose();
    }
}
