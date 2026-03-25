using Arch.Core;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol.Messages;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Protocol;

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
            TileLightLevels = new int[total],
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
            msg.TileLightLevels[idx] = tile.LightLevel;
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

    public static EntityMsg[] SerializeEntities(World world)
    {
        var entities = new List<EntityMsg>();
        var query = new QueryDescription().WithAll<Position, TileAppearance>();
        world.Query(in query, (Entity e, ref Position ePos, ref TileAppearance appearance) =>
        {
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
            entities.Add(msg);
        });
        return entities.ToArray();
    }

    public static EntityUpdateMsg[] SerializeEntityUpdates(World world)
    {
        var entities = new List<EntityUpdateMsg>();
        var query = new QueryDescription().WithAll<Position, TileAppearance>();
        world.Query(in query, (Entity e, ref Position ePos, ref TileAppearance appearance) =>
        {
            var update = new EntityUpdateMsg
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
                update.Health = health.Current;
                update.MaxHealth = health.Max;
            }
            entities.Add(update);
        });
        return entities.ToArray();
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

    public static PlayerHudMsg? BuildPlayerHud(GameEngine engine, Entity playerEntity)
    {
        var hudData = engine.GetPlayerHudData(playerEntity);
        if (hudData == null) return null;
        return new PlayerHudMsg
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
            FloorItemNames = hudData.FloorItemNames,
            EquippedWeaponName = hudData.EquippedWeaponName,
            EquippedArmorName = hudData.EquippedArmorName,
        };
    }
}
