using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.State;

/// <summary>
/// Client-side representation of the game world.
/// Updated from server snapshots/deltas. Used by the renderer.
/// </summary>
public class ClientGameState
{
    private readonly Dictionary<long, Chunk> _chunks = new();
    private readonly Dictionary<long, ClientEntity> _entities = new();

    public int PlayerX { get; set; }
    public int PlayerY { get; set; }
    public long WorldTick { get; set; }
    public IReadOnlyDictionary<long, ClientEntity> Entities => _entities;
    public PlayerHudMsg? PlayerHud { get; set; }

    public void Clear()
    {
        _chunks.Clear();
        _entities.Clear();
        PlayerX = 0;
        PlayerY = 0;
        WorldTick = 0;
        PlayerHud = null;
    }

    public void ApplySnapshot(WorldSnapshotMsg snapshot)
    {
        WorldTick = snapshot.WorldTick;
        PlayerX = snapshot.PlayerX;
        PlayerY = snapshot.PlayerY;

        _chunks.Clear();
        foreach (var chunkMsg in snapshot.Chunks)
            ApplyChunkData(chunkMsg);

        _entities.Clear();
        foreach (var entityMsg in snapshot.Entities)
        {
            _entities[entityMsg.Id] = new ClientEntity
            {
                Id = entityMsg.Id,
                X = entityMsg.X,
                Y = entityMsg.Y,
                GlyphId = entityMsg.GlyphId,
                FgColor = entityMsg.FgColor,
                Health = entityMsg.Health,
                MaxHealth = entityMsg.MaxHealth,
            };
        }

        PlayerHud = snapshot.PlayerHud;
    }

    public void ApplyDelta(WorldDeltaMsg delta)
    {
        WorldTick = delta.ToTick;

        // Update chunks (light levels change every tick)
        foreach (var chunkMsg in delta.Chunks)
            ApplyChunkData(chunkMsg);

        // Update tiles
        foreach (var tileUpdate in delta.TileUpdates)
        {
            var (cx, cy) = Chunk.WorldToChunkCoord(tileUpdate.X, tileUpdate.Y);
            long key = Chunk.PackChunkKey(cx, cy);
            if (_chunks.TryGetValue(key, out var chunk))
            {
                int lx = tileUpdate.X - cx * Chunk.Size;
                int ly = tileUpdate.Y - cy * Chunk.Size;
                if (chunk.InBounds(lx, ly))
                {
                    ref var tile = ref chunk.Tiles[lx, ly];
                    tile.Type = (TileType)tileUpdate.TileType;
                    tile.GlyphId = tileUpdate.GlyphId;
                    tile.FgColor = tileUpdate.FgColor;
                    tile.BgColor = tileUpdate.BgColor;
                    tile.LightLevel = tileUpdate.LightLevel;
                }
            }
        }

        // Update entities (delta-compressed: only changed/new/removed entities are included)
        foreach (var entityUpdate in delta.EntityUpdates)
        {
            if (entityUpdate.Removed)
            {
                _entities.Remove(entityUpdate.Id);
                continue;
            }

            if (!_entities.TryGetValue(entityUpdate.Id, out var entity))
            {
                entity = new ClientEntity { Id = entityUpdate.Id };
                _entities[entityUpdate.Id] = entity;
            }

            entity.X = entityUpdate.X;
            entity.Y = entityUpdate.Y;
            entity.GlyphId = entityUpdate.GlyphId;
            entity.FgColor = entityUpdate.FgColor;
            entity.Health = entityUpdate.Health;
            entity.MaxHealth = entityUpdate.MaxHealth;
        }

        // Find player entity and update position
        foreach (var entity in _entities.Values)
        {
            // The player entity has a specific glyph (@ = 64)
            if (entity.GlyphId == 64)
            {
                PlayerX = entity.X;
                PlayerY = entity.Y;
                break;
            }
        }

        if (delta.PlayerHud != null)
            PlayerHud = delta.PlayerHud;
    }

    private void ApplyChunkData(ChunkDataMsg msg)
    {
        var chunk = new Chunk(msg.ChunkX, msg.ChunkY);
        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
        {
            int idx = y * Chunk.Size + x;
            ref var tile = ref chunk.Tiles[x, y];
            tile.Type = (TileType)msg.TileTypes[idx];
            tile.GlyphId = msg.TileGlyphs[idx];
            tile.FgColor = msg.TileFgColors[idx];
            tile.BgColor = msg.TileBgColors[idx];
            tile.LightLevel = msg.TileLightLevels[idx];
        }
        long key = Chunk.PackChunkKey(msg.ChunkX, msg.ChunkY);
        _chunks[key] = chunk;
    }

    public TileInfo GetTile(int worldX, int worldY)
    {
        var (cx, cy) = Chunk.WorldToChunkCoord(worldX, worldY);
        long key = Chunk.PackChunkKey(cx, cy);
        if (!_chunks.TryGetValue(key, out var chunk))
            return default;
        int lx = worldX - cx * Chunk.Size;
        int ly = worldY - cy * Chunk.Size;
        if (!chunk.InBounds(lx, ly))
            return default;
        return chunk.Tiles[lx, ly];
    }
}

public class ClientEntity
{
    public long Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int GlyphId { get; set; }
    public int FgColor { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
}
