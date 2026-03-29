using RogueLikeNet.Core.Algorithms;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
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
    private readonly List<CombatEventMsg> _pendingCombatEvents = new();
    private readonly HashSet<long> _exploredTiles = new();
    private readonly HashSet<long> _visibleTiles = new();

    /// <summary>Debug: when true, all tiles are treated as visible and explored.</summary>
    public bool DebugSeeAll { get; set; }

    public int PlayerX { get; private set; }
    public int PlayerY { get; private set; }
    public long PlayerEntityId { get; private set; }
    public long WorldTick { get; private set; }
    public IReadOnlyDictionary<long, ClientEntity> Entities => _entities;
    public IReadOnlyDictionary<long, Chunk> Chunks => _chunks;
    public PlayerStateMsg? PlayerState { get; private set; }
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
        PlayerEntityId = 0;
        WorldTick = 0;
        PlayerState = null;
    }

    public void ApplyDelta(WorldDeltaMsg delta)
    {
        // Snapshot delta: clear transient state before applying (explored tiles persist for fog of war)
        if (delta.IsSnapshot)
        {
            _chunks.Clear();
            _entities.Clear();
            _pendingCombatEvents.Clear();
            _visibleTiles.Clear();
        }

        WorldTick = delta.ToTick;

        // Update chunks (full data for newly discovered chunks)
        foreach (var chunkMsg in delta.Chunks)
            ApplyChunkData(chunkMsg);

        // Discard chunks evicted by the server's LRU tracker
        foreach (var key in delta.DiscardedChunkKeys)
            _chunks.Remove(key);

        // Update tiles
        foreach (var tileUpdate in delta.TileUpdates)
        {
            var (cx, cy) = Chunk.WorldToChunkCoord(tileUpdate.X, tileUpdate.Y);
            long key = Position.PackCoord(cx, cy);
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
                    chunk.LightLevels[lx, ly] = tileUpdate.LightLevel;
                }
            }
        }

        // Update entities — full updates (new or changed)
        foreach (var entityUpdate in delta.EntityUpdates)
        {
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
            entity.Item = entityUpdate.Item;
        }

        // Position-health-only updates (X, Y, Health changed)
        foreach (var posHealthUpdate in delta.EntityPositionHealthUpdates)
        {
            if (_entities.TryGetValue(posHealthUpdate.Id, out var entity))
            {
                entity.X = posHealthUpdate.X;
                entity.Y = posHealthUpdate.Y;
                entity.Health = posHealthUpdate.Health;
            }
        }

        // Entity removals
        foreach (var removal in delta.EntityRemovals)
            _entities.Remove(removal.Id);

        if (delta.PlayerState != null)
            PlayerState = delta.PlayerState;

        // Queue combat events for particle system
        if (delta.CombatEvents.Length > 0)
            _pendingCombatEvents.AddRange(delta.CombatEvents);

        // Update player data (entity id, X, Y) and recompute visibility/lighting since it may have changed
        if (PlayerState != null)
            PlayerEntityId = PlayerState.PlayerEntityId;

        if (_entities.TryGetValue(PlayerEntityId, out var playerEntity))
        {
            PlayerX = playerEntity.X;
            PlayerY = playerEntity.Y;
        }

        ComputeVisibility();
        ComputeLighting();
    }

    public void DrainCombatEvents()
    {
        _pendingCombatEvents.Clear();
    }

    /// <summary>
    /// Returns floor items at the player's current position.
    /// </summary>
    public (int ItemTypeId, int Rarity)[] GetFloorItems()
    {
        var items = new List<(int, int)>();
        foreach (var entity in _entities.Values)
        {
            if (entity.Item != null && entity.X == PlayerX && entity.Y == PlayerY)
                items.Add((entity.Item.ItemTypeId, entity.Item.Rarity));
        }
        return items.ToArray();
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
        long key = Position.PackCoord(msg.ChunkX, msg.ChunkY);
        _chunks[key] = chunk;
    }

    public TileInfo GetTile(int worldX, int worldY)
    {
        return GetTileAndLightLevel(worldX, worldY).Item1;
    }

    public int GetLightLevel(int worldX, int worldY)
    {
        return GetTileAndLightLevel(worldX, worldY).Item2;
    }

    public (TileInfo, int) GetTileAndLightLevel(int worldX, int worldY)
    {
        var (cx, cy) = Chunk.WorldToChunkCoord(worldX, worldY);
        long key = Position.PackCoord(cx, cy);
        if (!_chunks.TryGetValue(key, out var chunk))
            return (default, 0);
        int lx = worldX - cx * Chunk.Size;
        int ly = worldY - cy * Chunk.Size;
        if (!chunk.InBounds(lx, ly))
            return (default, 0);
        return (chunk.Tiles[lx, ly], chunk.LightLevels[lx, ly]);
    }

    public bool IsExplored(int worldX, int worldY) =>
        DebugSeeAll || _exploredTiles.Contains(Position.PackCoord(worldX, worldY));

    public bool IsVisible(int worldX, int worldY) =>
        DebugSeeAll || _visibleTiles.Contains(Position.PackCoord(worldX, worldY));

    private void ComputeVisibility()
    {
        _visibleTiles.Clear();
        ShadowCastFov.Compute(PlayerX, PlayerY, ClassDefinitions.FOVRadius,
            isOpaque: (x, y) => !GetTile(x, y).IsTransparent,
            markVisible: (x, y) =>
            {
                long key = Position.PackCoord(x, y);
                _visibleTiles.Add(key);
                _exploredTiles.Add(key);
            });
    }

    private void ComputeLighting()
    {
        // Reset light only for chunks within FOV range of the player
        var (minCx, minCy) = Chunk.WorldToChunkCoord(PlayerX - ClassDefinitions.FOVRadius, PlayerY - ClassDefinitions.FOVRadius);
        var (maxCx, maxCy) = Chunk.WorldToChunkCoord(PlayerX + ClassDefinitions.FOVRadius, PlayerY + ClassDefinitions.FOVRadius);
        foreach (var chunk in _chunks.Values)
            if (chunk.ChunkX >= minCx && chunk.ChunkX <= maxCx && chunk.ChunkY >= minCy && chunk.ChunkY <= maxCy)
                chunk.ResetLight();

        // Player emits light at FOV radius
        FloodLight(PlayerX, PlayerY, ClassDefinitions.FOVRadius);

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
                long key = Position.PackCoord(cx, cy);
                if (!_chunks.TryGetValue(key, out var chunk)) return;

                int lx = x - cx * Chunk.Size;
                int ly = y - cy * Chunk.Size;
                if (!chunk.InBounds(lx, ly)) return;

                chunk.LightLevels[lx, ly] = Math.Max(chunk.LightLevels[lx, ly], lightAmount);
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
    public ItemDataMsg? Item { get; set; }
}
