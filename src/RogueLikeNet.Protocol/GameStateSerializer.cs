using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
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
            TileTypes = new byte[total],
            TileGlyphs = new int[total],
            TileFgColors = new int[total],
            TileBgColors = new int[total],
            TilePlaceableItemIds = new int[total],
            TilePlaceableItemExtras = new int[total],
        };

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                int idx = y * Chunk.Size + x;
                ref var tile = ref chunk.Tiles[x, y];
                msg.TileTypes[idx] = (byte)tile.Type;
                msg.TileGlyphs[idx] = tile.GlyphId;
                msg.TileFgColors[idx] = tile.FgColor;
                msg.TileBgColors[idx] = tile.BgColor;
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

        void ProcessEntity(long id, Position pos, TileAppearance appearance,
            int hp, int maxHp, int lightRadius, ItemDataMsg? item)
        {
            if (!debugVisibilityOff && !fov.IsVisible(pos)) return;

            currentIds.Add(id);

            var current = new EntityUpdateMsg
            {
                Id = id,
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
            ProcessEntity((long)p.Id, p.Position, p.Appearance, p.Health.Current, p.Health.Max, 0, null);
        }

        // Iterate loaded chunks
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (var m in chunk.Monsters)
            {
                if (m.IsDead) continue;
                ProcessEntity((long)m.Id, m.Position, m.Appearance, m.Health.Current, m.Health.Max, 0, null);
            }

            foreach (var gi in chunk.GroundItems)
            {
                if (gi.IsDestroyed) continue;
                var item = new ItemDataMsg
                {
                    ItemTypeId = gi.Item.ItemTypeId,
                    Rarity = gi.Item.Rarity,
                    Category = ItemDefinitions.Get(gi.Item.ItemTypeId).Category,
                    StackCount = gi.Item.StackCount,
                    BonusAttack = gi.Item.BonusAttack,
                    BonusDefense = gi.Item.BonusDefense,
                    BonusHealth = gi.Item.BonusHealth,
                };
                ProcessEntity((long)gi.Id, gi.Position, gi.Appearance, 0, 0, 0, item);
            }

            foreach (var r in chunk.ResourceNodes)
            {
                if (r.IsDead) continue;
                ProcessEntity((long)r.Id, r.Position, r.Appearance, r.Health.Current, r.Health.Max, 0, null);
            }

            foreach (var n in chunk.TownNpcs)
            {
                if (n.IsDead) continue;
                ProcessEntity((long)n.Id, n.Position, n.Appearance, n.Health.Current, n.Health.Max, 0, null);
            }

            foreach (var e in chunk.Elements)
            {
                int lightRadius = e.Light.HasValue ? e.Light.Value.Radius : 0;
                ProcessEntity((long)e.Id, e.Position, e.Appearance, 0, 0, lightRadius, null);
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
            InventoryCount = stateData.InventoryCount,
            InventoryCapacity = stateData.InventoryCapacity,
            Skills = stateData.Skills.Select(s => new SkillSlotMsg { Id = s.Id, Cooldown = s.Cooldown, Name = s.Name }).ToArray(),
            InventoryItems = stateData.InventoryItems.Select(i => new ItemDataMsg { ItemTypeId = i.ItemTypeId, StackCount = i.StackCount, Rarity = i.Rarity, Category = i.Category, BonusAttack = i.BonusAttack, BonusDefense = i.BonusDefense, BonusHealth = i.BonusHealth }).ToArray(),
            EquippedWeapon = stateData.EquippedWeapon.HasValue ? new ItemDataMsg { ItemTypeId = stateData.EquippedWeapon.Value.ItemTypeId, StackCount = stateData.EquippedWeapon.Value.StackCount, Rarity = stateData.EquippedWeapon.Value.Rarity, Category = stateData.EquippedWeapon.Value.Category, BonusAttack = stateData.EquippedWeapon.Value.BonusAttack, BonusDefense = stateData.EquippedWeapon.Value.BonusDefense, BonusHealth = stateData.EquippedWeapon.Value.BonusHealth } : null,
            EquippedArmor = stateData.EquippedArmor.HasValue ? new ItemDataMsg { ItemTypeId = stateData.EquippedArmor.Value.ItemTypeId, StackCount = stateData.EquippedArmor.Value.StackCount, Rarity = stateData.EquippedArmor.Value.Rarity, Category = stateData.EquippedArmor.Value.Category, BonusAttack = stateData.EquippedArmor.Value.BonusAttack, BonusDefense = stateData.EquippedArmor.Value.BonusDefense, BonusHealth = stateData.EquippedArmor.Value.BonusHealth } : null,
            QuickSlotIndices = stateData.QuickSlotIndices,
            PlayerEntityId = (long)player.Id,
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
