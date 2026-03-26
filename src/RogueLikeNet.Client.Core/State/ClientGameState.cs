using RogueLikeNet.Core.Algorithms;
using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.State;

/// <summary>
/// Client-side representation of the game world.
/// Updated from server snapshots/deltas. Used by the renderer.
/// </summary>
public class ClientGameState
{
    private const int FovRadius = 20;
    private readonly Dictionary<long, Chunk> _chunks = new();
    private readonly Dictionary<long, ClientEntity> _entities = new();
    private readonly List<CombatEventMsg> _pendingCombatEvents = new();
    private readonly HashSet<long> _exploredTiles = new();
    private readonly HashSet<long> _visibleTiles = new();

    public int PlayerX { get; set; }
    public int PlayerY { get; set; }
    public long WorldTick { get; set; }
    public IReadOnlyDictionary<long, ClientEntity> Entities => _entities;
    public IReadOnlyDictionary<long, Chunk> Chunks => _chunks;
    public PlayerStateMsg? PlayerState { get; set; }
    public FloorItemsMsg? FloorItems { get; set; }
    public IReadOnlyList<CombatEventMsg> PendingCombatEvents => _pendingCombatEvents;

    public void Clear()
    {
        _chunks.Clear();
        _entities.Clear();
        _pendingCombatEvents.Clear();
        _exploredTiles.Clear();
        _visibleTiles.Clear();
        PlayerX = 0;
        PlayerY = 0;
        WorldTick = 0;
        PlayerState = null;
        FloorItems = null;
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
                LightRadius = entityMsg.LightRadius,
            };
        }

        PlayerState = snapshot.PlayerState;
        FloorItems = snapshot.FloorItems;
        ComputeVisibility();
        ComputeLighting();
    }

    public void ApplyDelta(WorldDeltaMsg delta)
    {
        WorldTick = delta.ToTick;

        // Update chunks (full data for newly discovered chunks)
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
            entity.LightRadius = entityUpdate.LightRadius;
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

        if (delta.PlayerState != null)
            PlayerState = delta.PlayerState;

        if (delta.FloorItems != null)
            FloorItems = delta.FloorItems;

        // Queue combat events for particle system
        if (delta.CombatEvents.Length > 0)
            _pendingCombatEvents.AddRange(delta.CombatEvents);

        ComputeVisibility();
        ComputeLighting();
    }

    public void DrainCombatEvents()
    {
        _pendingCombatEvents.Clear();
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

    public bool IsExplored(int worldX, int worldY) =>
        _exploredTiles.Contains(((long)worldX << 32) | (uint)worldY);

    public bool IsVisible(int worldX, int worldY) =>
        _visibleTiles.Contains(((long)worldX << 32) | (uint)worldY);

    private void ComputeVisibility()
    {
        _visibleTiles.Clear();
        ShadowCastFov.Compute(PlayerX, PlayerY, FovRadius,
            isOpaque: (x, y) => !GetTile(x, y).IsTransparent,
            markVisible: (x, y) =>
            {
                long key = ((long)x << 32) | (uint)y;
                _visibleTiles.Add(key);
                _exploredTiles.Add(key);
            });
    }

    private void ComputeLighting()
    {
        // Reset all loaded chunk light to 0
        foreach (var chunk in _chunks.Values)
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                    chunk.Tiles[x, y].LightLevel = 0;

        // Player emits light at FOV radius
        FloodLight(PlayerX, PlayerY, FovRadius);

        // Light source entities
        foreach (var entity in _entities.Values)
            if (entity.LightRadius > 0)
                FloodLight(entity.X, entity.Y, entity.LightRadius);
    }

    private void FloodLight(int originX, int originY, int radius)
    {
        ShadowCastFov.Compute(originX, originY, radius,
            isOpaque: (x, y) => !GetTile(x, y).IsTransparent,
            markVisible: (x, y) =>
            {
                int dx = x - originX;
                int dy = y - originY;
                int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                int lightAmount = (radius - dist + 1) * 10 / (radius + 1);
                if (lightAmount <= 0) return;

                var (cx, cy) = Chunk.WorldToChunkCoord(x, y);
                long key = Chunk.PackChunkKey(cx, cy);
                if (!_chunks.TryGetValue(key, out var chunk)) return;

                int lx = x - cx * Chunk.Size;
                int ly = y - cy * Chunk.Size;
                if (!chunk.InBounds(lx, ly)) return;

                ref var tile = ref chunk.Tiles[lx, ly];
                tile.LightLevel = Math.Max(tile.LightLevel, lightAmount);
            });
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
    public int LightRadius { get; set; }
}
