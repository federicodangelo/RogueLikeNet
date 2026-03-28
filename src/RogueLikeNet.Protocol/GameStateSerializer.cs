using Arch.Core;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
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
        GameEngine engine, int worldX, int worldY, HashSet<long> sentChunkKeys, int chunkRange)
    {
        var (cx, cy) = Chunk.WorldToChunkCoord(worldX, worldY);
        var newChunks = new List<ChunkDataMsg>();

        for (int dx = -chunkRange; dx <= chunkRange; dx++)
            for (int dy = -chunkRange; dy <= chunkRange; dy++)
            {
                int ccx = cx + dx, ccy = cy + dy;
                long key = Position.PackCoord(ccx, ccy);

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
            var (kx, ky) = Position.UnpackCoord(key);
            return Math.Abs(kx - cx) > chunkRange + 1 || Math.Abs(ky - cy) > chunkRange + 1;
        });

        return newChunks.ToArray();
    }

    /// <summary>
    /// Builds a delta-compressed entity update for a specific player.
    /// Only includes entities visible to the player's FOV, and marks
    /// previously-visible entities that left FOV as removed.
    /// Returns updated previousState for the next tick.
    /// </summary>
    public static SerializedEntityData SerializeEntityDelta(World world, FOVData fov,
            Dictionary<long, EntityUpdateMsg> previousState)
    {
        var fullUpdates = new List<EntityUpdateMsg>();
        var positionHealthUpdates = new List<EntityPositionHealthMsg>();
        var removals = new List<EntityRemovedMsg>();
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
            ItemDataMsg? item = null;
            if (world.Has<ItemData>(e))
            {
                var itemData = world.Get<ItemData>(e);
                item = new ItemDataMsg
                {
                    ItemTypeId = itemData.ItemTypeId,
                    Rarity = itemData.Rarity,
                    Category = ItemDefinitions.Get(itemData.ItemTypeId).Category,
                    StackCount = itemData.StackCount,
                    BonusAttack = itemData.BonusAttack,
                    BonusDefense = itemData.BonusDefense,
                    BonusHealth = itemData.BonusHealth,
                };
            }

            var current = new EntityUpdateMsg
            {
                Id = id,
                X = ePos.X,
                Y = ePos.Y,
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
                        positionHealthUpdates.Add(new EntityPositionHealthMsg { Id = id, X = ePos.X, Y = ePos.Y, Health = hp });
                    else
                        fullUpdates.Add(current);
                }
            }
            else
            {
                // New entity
                fullUpdates.Add(current);
            }

            previousState[id] = current;
        });

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
        var stateData = engine.GetPlayerStateData(playerEntity);
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
            PlayerEntityId = playerEntity.Id,
        };
    }
}

public readonly record struct SerializedEntityData(EntityUpdateMsg[] FullUpdates, EntityPositionHealthMsg[] PositionHealthUpdates, EntityRemovedMsg[] Removals);
