using Arch.Core;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol.Messages;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Protocol;

/// <summary>
/// Compact snapshot of entity state for delta comparison.
/// </summary>
public readonly record struct EntitySnapshot(int X, int Y, int GlyphId, int FgColor, int Health, int MaxHealth, int LightRadius);

/// <summary>
/// Shared helpers for building snapshot/delta/HUD messages from game state.
/// Used by both GameLoop (server) and LocalGameConnection (web client).
/// </summary>
public static class GameStateSerializer
{
    public static ChunkDataMsg SerializeChunk(Chunk chunk)
    {
        int total = Chunk.Size * Chunk.Size;
        var msg = new ChunkDataMsg
        {
            ChunkX = chunk.ChunkX,
            ChunkY = chunk.ChunkY,
            TileTypes = new byte[total],
            TileGlyphs = new int[total],
            TileFgColors = new int[total],
            TileBgColors = new int[total],
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
            }

        return msg;
    }

    public static ChunkDataMsg[] SerializeChunksAroundPosition(GameEngine engine, int worldX, int worldY)
    {
        var (cx, cy) = Chunk.WorldToChunkCoord(worldX, worldY);
        var chunks = new List<ChunkDataMsg>();
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var chunk = engine.EnsureChunkLoaded(cx + dx, cy + dy);
                chunks.Add(SerializeChunk(chunk));
            }
        return chunks.ToArray();
    }

    /// <summary>
    /// Delta-aware chunk serialization: sends full ChunkDataMsg only for new chunks.
    /// Already-sent chunks are skipped since static data doesn't change and
    /// lighting is computed client-side.
    /// </summary>
    public static ChunkDataMsg[] SerializeChunksDelta(
        GameEngine engine, int worldX, int worldY, HashSet<long> sentChunkKeys)
    {
        var (cx, cy) = Chunk.WorldToChunkCoord(worldX, worldY);
        var newChunks = new List<ChunkDataMsg>();

        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int ccx = cx + dx, ccy = cy + dy;
                long key = Chunk.PackChunkKey(ccx, ccy);

                if (!sentChunkKeys.Contains(key))
                {
                    var chunk = engine.EnsureChunkLoaded(ccx, ccy);
                    newChunks.Add(SerializeChunk(chunk));
                    sentChunkKeys.Add(key);
                }
            }

        // Prune chunk keys far from current position (5×5 around player chunk)
        sentChunkKeys.RemoveWhere(key =>
        {
            int kx = (int)(key >> 32);
            int ky = (int)(key & 0xFFFFFFFF);
            return Math.Abs(kx - cx) > 2 || Math.Abs(ky - cy) > 2;
        });

        return newChunks.ToArray();
    }

    public static EntityMsg[] SerializeEntities(World world, FOVData fov)
    {
        var entities = new List<EntityMsg>();
        var query = new QueryDescription().WithAll<Position, TileAppearance>();
        world.Query(in query, (Entity e, ref Position ePos, ref TileAppearance appearance) =>
        {
            if (!fov.IsVisible(ePos.X, ePos.Y)) return;
            var msg = new EntityMsg
            {
                Id = e.Id,
                X = ePos.X,
                Y = ePos.Y,
                GlyphId = appearance.GlyphId,
                FgColor = appearance.FgColor,
            };
            if (world.Has<Health>(e))
            {
                ref var health = ref world.Get<Health>(e);
                msg.Health = health.Current;
                msg.MaxHealth = health.Max;
            }
            if (world.Has<LightSource>(e))
                msg.LightRadius = world.Get<LightSource>(e).Radius;
            entities.Add(msg);
        });
        return entities.ToArray();
    }

    /// <summary>
    /// Builds a delta-compressed entity update for a specific player.
    /// Only includes entities visible to the player's FOV, and marks
    /// previously-visible entities that left FOV as Removed.
    /// Returns updated previousState for the next tick.
    /// </summary>
    public static EntityUpdateMsg[] SerializeEntityUpdatesDelta(
        World world, FOVData fov,
        Dictionary<long, EntitySnapshot> previousState)
    {
        var updates = new List<EntityUpdateMsg>();
        var currentIds = new HashSet<long>();
        var query = new QueryDescription().WithAll<Position, TileAppearance>();

        world.Query(in query, (Entity e, ref Position ePos, ref TileAppearance appearance) =>
        {
            if (!fov.IsVisible(ePos.X, ePos.Y)) return;

            long id = e.Id;
            currentIds.Add(id);

            int hp = 0, maxHp = 0;
            if (world.Has<Health>(e))
            {
                ref var health = ref world.Get<Health>(e);
                hp = health.Current;
                maxHp = health.Max;
            }
            int lightRadius = world.Has<LightSource>(e) ? world.Get<LightSource>(e).Radius : 0;

            var snap = new EntitySnapshot(ePos.X, ePos.Y, appearance.GlyphId, appearance.FgColor, hp, maxHp, lightRadius);

            // Only send if changed or new
            if (!previousState.TryGetValue(id, out var prev) || prev != snap)
            {
                updates.Add(new EntityUpdateMsg
                {
                    Id = id,
                    X = ePos.X,
                    Y = ePos.Y,
                    GlyphId = appearance.GlyphId,
                    FgColor = appearance.FgColor,
                    Health = hp,
                    MaxHealth = maxHp,
                    LightRadius = lightRadius,
                });
            }

            previousState[id] = snap;
        });

        // Entities that were visible last tick but are no longer → mark Removed
        var staleIds = previousState.Keys.Where(id => !currentIds.Contains(id)).ToList();
        foreach (var id in staleIds)
        {
            updates.Add(new EntityUpdateMsg { Id = id, Removed = true });
            previousState.Remove(id);
        }

        return updates.ToArray();
    }

    public static CombatEventMsg[] SerializeCombatEvents(GameEngine engine)
    {
        var combatEvents = engine.Combat.LastTickEvents;
        if (combatEvents.Count == 0) return [];
        return combatEvents.Select(e => new CombatEventMsg
        {
            AttackerX = e.AttackerX,
            AttackerY = e.AttackerY,
            TargetX = e.TargetX,
            TargetY = e.TargetY,
            Damage = e.Damage,
            TargetDied = e.TargetDied,
        }).ToArray();
    }

    public static PlayerStateMsg? BuildPlayerState(GameEngine engine, Entity playerEntity)
    {
        var hudData = engine.GetPlayerStateData(playerEntity);
        if (hudData == null) return null;
        return new PlayerStateMsg
        {
            Health = hudData.Health,
            MaxHealth = hudData.MaxHealth,
            Attack = hudData.Attack,
            Defense = hudData.Defense,
            Level = hudData.Level,
            Experience = hudData.Experience,
            InventoryCount = hudData.InventoryCount,
            InventoryCapacity = hudData.InventoryCapacity,
            SkillIds = hudData.SkillIds,
            SkillCooldowns = hudData.SkillCooldowns,
            SkillNames = hudData.SkillNames,
            InventoryNames = hudData.InventoryNames,
            InventoryStackCounts = hudData.InventoryStackCounts,
            InventoryRarities = hudData.InventoryRarities,
            InventoryCategories = hudData.InventoryCategories,
            EquippedWeaponName = hudData.EquippedWeaponName,
            EquippedArmorName = hudData.EquippedArmorName,
            QuickSlotIndices = hudData.QuickSlotIndices,
            PlayerEntityId = playerEntity.Id,
        };
    }

    public static FloorItemsMsg? BuildFloorItems(GameEngine engine, Entity playerEntity)
    {
        var names = engine.GetFloorItemsData(playerEntity);
        return new FloorItemsMsg { Names = names };
    }
}
