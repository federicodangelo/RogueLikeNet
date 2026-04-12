using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.Utilities;
using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol.Messages;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Protocol;

/// <summary>
/// Shared helpers for building snapshot/delta/HUD messages from game state.
/// </summary>
public static class GameStateSerializer
{
    public static ChunkDataMsg SerializeChunk(Chunk chunk)
    {
        int total = Chunk.Size * Chunk.Size;
        var msg = new ChunkDataMsg
        {
            ChunkX = chunk.ChunkPosition.X,
            ChunkY = chunk.ChunkPosition.Y,
            ChunkZ = chunk.ChunkPosition.Z,
            TileIds = new int[total],
            TilePlaceableItemIds = new int[total],
            TilePlaceableItemExtras = new int[total],
        };

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                int idx = y * Chunk.Size + x;
                ref var tile = ref chunk.Tiles[x, y];
                msg.TileIds[idx] = tile.TileId;
                msg.TilePlaceableItemIds[idx] = tile.PlaceableItemId;
                msg.TilePlaceableItemExtras[idx] = tile.PlaceableItemExtra;
            }

        return msg;
    }

    public static ChunkDataMsg[] SerializeChunksAroundPosition(GameEngine engine, Position pos)
    {
        var chunkPos = Chunk.WorldToChunkCoord(pos);
        var chunks = new List<ChunkDataMsg>();
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var chunk = engine.EnsureChunkLoaded(ChunkPosition.FromCoords(chunkPos.X + dx, chunkPos.Y + dy, chunkPos.Z));
                chunks.Add(SerializeChunk(chunk));
            }
        return chunks.ToArray();
    }

    /// <summary>
    /// Delta-aware chunk serialization using an LRU tracker.
    /// Sends full ChunkDataMsg only for new chunks. Already-tracked chunks are
    /// touched (promoted in LRU). After adding new chunks, the tracker evicts
    /// least-recently-used entries beyond its capacity, returning their keys so
    /// the client can discard them.
    /// </summary>
    public static ChunkDeltaResult SerializeChunksDelta(
        GameEngine engine, Position pos, ChunkTracker tracker,
        int visibleChunks, int maxChunksToSerialize = int.MaxValue)
    {
        tracker.UpdateCapacity(visibleChunks);
        var chunkRange = ChunkTracker.ComputeChunkRange(visibleChunks);
        var chunkPos = Chunk.WorldToChunkCoord(pos);
        var newChunks = new List<ChunkDataMsg>();

        foreach (var z in PointsAtDistance.GetZLevels(1))
        {
            int ccz = chunkPos.Z + z;
            if (ccz < 0 || ccz > 255) continue;
            foreach (var point in PointsAtDistance.GetPoints(chunkRange))
            {
                int ccx = chunkPos.X + point.X, ccy = chunkPos.Y + point.Y;
                long key = ChunkPosition.PackCoord(ccx, ccy, ccz);

                if (tracker.Touch(key))
                {
                    var chunk = engine.EnsureChunkLoadedOrDoesntExist(ChunkPosition.FromCoords(ccx, ccy, ccz));
                    if (chunk != null)
                    {
                        newChunks.Add(SerializeChunk(chunk));
                        maxChunksToSerialize--;
                        if (maxChunksToSerialize <= 0)
                            break;
                    }
                }
            }
        }

        var discarded = tracker.Evict();

        return new ChunkDeltaResult(newChunks.ToArray(), discarded);
    }

    /// <summary>
    /// Builds a delta-compressed entity update for a specific player.
    /// Only includes entities visible to the player's FOV, and marks
    /// previously-visible entities that left FOV as removed.
    /// Returns updated previousState for the next tick.
    /// </summary>
    public static SerializedEntityData SerializeEntityDelta(WorldMap map, FOVData fov,
            Dictionary<long, EntityUpdateMsg> previousState, bool debugVisibilityOff = false)
    {
        var fullUpdates = new List<EntityUpdateMsg>();
        var positionHealthUpdates = new List<EntityPositionHealthMsg>();
        var removals = new List<EntityRemovedMsg>();
        var currentIds = new HashSet<long>();

        void ProcessEntity(long id, EntityType entityType, Position pos, TileAppearance appearance,
            int hp, int maxHp, int lightRadius, ItemDataMsg? item)
        {
            if (!debugVisibilityOff && !fov.IsVisible(pos)) return;

            currentIds.Add(id);

            var current = new EntityUpdateMsg
            {
                Id = id,
                EntityType = (int)entityType,
                X = pos.X,
                Y = pos.Y,
                Z = pos.Z,
                GlyphId = appearance.GlyphId,
                FgColor = appearance.FgColor,
                Health = hp,
                MaxHealth = maxHp,
                LightRadius = lightRadius,
                Item = item,
            };

            if (previousState.TryGetValue(id, out var prev))
            {
                if (!current.SameValues(prev))
                {
                    if (current.HasOnlyPositionHealthChanges(prev))
                        positionHealthUpdates.Add(new EntityPositionHealthMsg { Id = id, X = pos.X, Y = pos.Y, Z = pos.Z, Health = hp });
                    else
                        fullUpdates.Add(current);
                }
            }
            else
            {
                fullUpdates.Add(current);
            }

            previousState[id] = current;
        }

        // Players
        foreach (var p in map.Players)
        {
            if (p.IsDead) continue;
            ProcessEntity((long)p.Id, EntityType.Player, p.Position, p.Appearance, p.Health.Current, p.Health.Max, 0, null);
        }

        // Iterate loaded chunks
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (var m in chunk.Monsters)
            {
                if (m.IsDead) continue;
                ProcessEntity((long)m.Id, EntityType.Monster, m.Position, m.Appearance, m.Health.Current, m.Health.Max, 0, null);
            }

            foreach (var gi in chunk.GroundItems)
            {
                if (gi.IsDestroyed) continue;
                var item = new ItemDataMsg
                {
                    ItemTypeId = gi.Item.ItemTypeId,
                    Category = GameData.Instance.Items.Get(gi.Item.ItemTypeId)?.CategoryInt ?? 0,
                    StackCount = gi.Item.StackCount,
                };
                ProcessEntity((long)gi.Id, EntityType.GroundItem, gi.Position, gi.Appearance, 0, 0, 0, item);
            }

            foreach (var r in chunk.ResourceNodes)
            {
                if (r.IsDead) continue;
                ProcessEntity((long)r.Id, EntityType.ResourceNode, r.Position, r.Appearance, r.Health.Current, r.Health.Max, 0, null);
            }

            foreach (var n in chunk.TownNpcs)
            {
                if (n.IsDead) continue;
                ProcessEntity((long)n.Id, EntityType.TownNpc, n.Position, n.Appearance, n.Health.Current, n.Health.Max, 0, null);
            }

            foreach (var c in chunk.Crops)
            {
                if (c.IsDestroyed) continue;
                ProcessEntity((long)c.Id, EntityType.Crop, c.Position, c.Appearance, 0, 0, 0, null);
            }

            foreach (var a in chunk.Animals)
            {
                if (a.IsDead) continue;
                ProcessEntity((long)a.Id, EntityType.Animal, a.Position, a.Appearance, a.Health.Current, a.Health.Max, 0, null);
            }
        }

        // Entities that were visible last tick but are no longer → removed
        var staleIds = previousState.Keys.Where(id => !currentIds.Contains(id)).ToList();
        foreach (var id in staleIds)
        {
            removals.Add(new EntityRemovedMsg { Id = id });
            previousState.Remove(id);
        }

        return new SerializedEntityData(fullUpdates.ToArray(), positionHealthUpdates.ToArray(), removals.ToArray());
    }

    public static CombatEventMsg[] SerializeCombatEvents(GameEngine engine)
    {
        var combatEvents = engine.Combat.LastTickEvents;
        if (combatEvents.Count == 0) return [];
        return combatEvents.Select(e => new CombatEventMsg
        {
            AttackerX = e.Attacker.X,
            AttackerY = e.Attacker.Y,
            TargetX = e.Target.X,
            TargetY = e.Target.Y,
            Damage = e.Damage,
            TargetDied = e.TargetDied,
            Blocked = e.Blocked,
        }).ToArray();
    }

    public static NpcDialogueMsg[] SerializeNpcDialogueEvents(GameEngine engine)
    {
        var events = engine.Combat.LastTickDialogueEvents;
        if (events.Count == 0) return [];
        return events.Select(e => new NpcDialogueMsg
        {
            NpcX = e.Npc.X,
            NpcY = e.Npc.Y,
            NpcName = e.NpcName,
            Text = e.Text,
        }).ToArray();
    }

    public static PlayerActionEventMsg[] SerializePlayerActionEvents(PlayerEntity player)
    {
        if (player.ActionEvents.Count == 0) return [];
        return player.ActionEvents.Select(e => new PlayerActionEventMsg
        {
            EventType = (int)e.EventType,
            ItemTypeId = e.ItemTypeId,
            StackCount = e.StackCount,
            Failed = e.Failed,
            FailReason = (int)e.FailReason,
        }).ToArray();
    }

    public static PlayerStateMsg? BuildPlayerState(GameEngine engine, PlayerEntity player)
    {
        var stateData = engine.GetPlayerStateData(player);
        if (stateData == null) return null;
        return new PlayerStateMsg
        {
            Health = stateData.Health,
            MaxHealth = stateData.MaxHealth,
            Attack = stateData.Attack,
            Defense = stateData.Defense,
            Level = stateData.Level,
            Experience = stateData.Experience,
            Hunger = stateData.Hunger,
            MaxHunger = stateData.MaxHunger,
            Thirst = stateData.Thirst,
            MaxThirst = stateData.MaxThirst,
            InventoryCount = stateData.InventoryCount,
            InventoryCapacity = stateData.InventoryCapacity,
            InventoryItems = stateData.InventoryItems.Select(i => new ItemDataMsg { ItemTypeId = i.ItemTypeId, StackCount = i.StackCount, Category = i.Category }).ToArray(),
            EquippedItems = stateData.EquippedItems.Select(i => new ItemDataMsg { ItemTypeId = i.ItemTypeId, StackCount = i.StackCount, Category = i.Category, EquipSlot = i.EquipSlot }).ToArray(),
            QuickSlotIndices = stateData.QuickSlotIndices,
            PlayerEntityId = (long)player.Id,
            NearbyStationsTypes = stateData.NearbyStationsTypes,
        };
    }

    public static PlayerStateMsg? SerializePlayerStateDelta(GameEngine engine, PlayerEntity player, PlayerStateMsg? lastSentPlayerState)
    {
        var playerState = BuildPlayerState(engine, player);

        if (playerState == null) return null;

        if (lastSentPlayerState != null && lastSentPlayerState.Equals(playerState))
            return null;

        return playerState;
    }
}

public readonly record struct SerializedEntityData(EntityUpdateMsg[] FullUpdates, EntityPositionHealthMsg[] PositionHealthUpdates, EntityRemovedMsg[] Removals);

public readonly record struct ChunkDeltaResult(ChunkDataMsg[] NewChunks, long[] DiscardedKeys);
