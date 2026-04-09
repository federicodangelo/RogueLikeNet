using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using static RogueLikeNet.Core.Definitions.PlaceableDefinitions;

namespace RogueLikeNet.Core.World;

/// <summary>
/// Manages all chunks in the game world. Generates new chunks on demand.
/// Tracks dynamic tiles (e.g. open doors with auto-close timers) for efficient per-tick processing.
/// </summary>
public class WorldMap
{
    /// <summary>
    /// Grace period (in ticks) before an opened door auto-closes.
    /// Stored directly in TileInfo.PlaceableItemExtra so it survives save/load.
    /// </summary>
    public const int DoorGraceTicks = 20;

    private readonly Dictionary<long, Chunk> _chunks = new();
    private readonly HashSet<long> _chunksDontExist = new();
    private readonly long _seed;

    // ── Player storage (global, not per-chunk) ────────────────────────
    private readonly List<PlayerEntity> _players = new();
    private readonly Dictionary<long, int> _playersByConnection = new();
    private int _nextEntityId = 1;

    public Span<PlayerEntity> Players => CollectionsMarshal.AsSpan(_players);

    /// <summary>
    /// Tracks world coordinates of tiles that need per-tick processing, grouped by chunk key.
    /// Used to avoid scanning every tile every tick.
    /// </summary>
    private readonly Dictionary<long, HashSet<long>> _dynamicTilesByChunk = new();

    public long Seed => _seed;

    public WorldMap(long seed)
    {
        _seed = seed;
    }

    // ── Entity ID management ──────────────────────────────────────────

    public int AllocateEntityId() => _nextEntityId++;

    /// <summary>Sets the next entity ID (used when restoring from persistence).</summary>
    public void SetNextEntityId(int nextId) => _nextEntityId = nextId;

    // ── Player management ────────────────────────────────────────────

    public ref PlayerEntity AddPlayer(PlayerEntity player)
    {
        var index = _players.FindIndex(p => p.Id == player.Id);
        if (index >= 0)
        {
            _players[index] = player;
        }
        else
        {
            _players.Add(player);
            index = _players.Count - 1;
        }
        _playersByConnection[player.ConnectionId] = player.Id;
        return ref Players[index];
    }

    public void RemovePlayer(int entityId)
    {
        var connectionId = _players.Exists(p => p.Id == entityId) ? _players.First(p => p.Id == entityId).ConnectionId : (long?)null;

        if (connectionId.HasValue)
        {
            _playersByConnection.Remove(connectionId.Value);
            _players.RemoveAll(p => p.Id == entityId);
        }
    }

    public PlayerEntity? GetPlayer(int entityId) => _players.Exists(p => p.Id == entityId) ? _players.First(p => p.Id == entityId) : null;

    public ref PlayerEntity GetPlayerRef(int entityId)
    {
        var index = _players.FindIndex(p => p.Id == entityId);
        if (index == -1) throw new KeyNotFoundException($"Player with entity ID {entityId} not found.");
        return ref Players[index];
    }

    public PlayerEntity? GetPlayerByConnection(long connectionId)
        => _playersByConnection.TryGetValue(connectionId, out var id) ? GetPlayer(id) : null;

    public EntityRef GetEntityRefAt(Position pos)
    {
        foreach (var p in _players)
            if (!p.IsDead && p.Position == pos)
                return new EntityRef(p.Id, EntityType.Player);

        var chunk = GetChunkForWorldPos(pos);
        if (chunk == null) return default;

        foreach (var m in chunk.Monsters)
            if (!m.IsDead && m.Position == pos)
                return new EntityRef(m.Id, EntityType.Monster);

        foreach (var n in chunk.TownNpcs)
            if (!n.IsDead && n.Position == pos)
                return new EntityRef(n.Id, EntityType.TownNpc);

        foreach (var r in chunk.ResourceNodes)
            if (!r.IsDead && r.Position == pos)
                return new EntityRef(r.Id, EntityType.ResourceNode);

        foreach (var g in chunk.GroundItems)
            if (!g.IsDestroyed && g.Position == pos)
                return new EntityRef(g.Id, EntityType.GroundItem);

        return default;
    }

    private List<EntityRef> _tmpRefs = [];

    public EntityRef[] GetAllEntityRefsAt(Position pos)
    {
        _tmpRefs.Clear();
        var refs = _tmpRefs;

        foreach (var p in _players)
            if (!p.IsDead && p.Position == pos)
                refs.Add(new EntityRef(p.Id, EntityType.Player));

        var chunk = GetChunkForWorldPos(pos);
        if (chunk == null) return refs.Count > 0 ? refs.ToArray() : null;

        foreach (var m in chunk.Monsters)
            if (!m.IsDead && m.Position == pos)
                refs.Add(new EntityRef(m.Id, EntityType.Monster));

        foreach (var n in chunk.TownNpcs)
            if (!n.IsDead && n.Position == pos)
                refs.Add(new EntityRef(n.Id, EntityType.TownNpc));

        foreach (var r in chunk.ResourceNodes)
            if (!r.IsDead && r.Position == pos)
                refs.Add(new EntityRef(r.Id, EntityType.ResourceNode));

        foreach (var g in chunk.GroundItems)
            if (!g.IsDestroyed && g.Position == pos)
                refs.Add(new EntityRef(g.Id, EntityType.GroundItem));

        return refs.Count > 0 ? refs.ToArray() : [];
    }

    public ref MonsterEntity GetMonsterRef(int entityId)
    {
        foreach (var chunk in _chunks.Values)
        {
            var span = chunk.Monsters;
            for (int i = 0; i < span.Length; i++)
                if (span[i].Id == entityId) return ref span[i];
        }
        throw new KeyNotFoundException($"Monster entity {entityId} not found.");
    }

    public ref GroundItemEntity GetGroundItemRef(int entityId)
    {
        foreach (var chunk in _chunks.Values)
        {
            var span = chunk.GroundItems;
            for (int i = 0; i < span.Length; i++)
                if (span[i].Id == entityId) return ref span[i];
        }
        throw new KeyNotFoundException($"Ground item entity {entityId} not found.");
    }

    public ref ResourceNodeEntity GetResourceNodeRef(int entityId)
    {
        foreach (var chunk in _chunks.Values)
        {
            var span = chunk.ResourceNodes;
            for (int i = 0; i < span.Length; i++)
                if (span[i].Id == entityId) return ref span[i];
        }
        throw new KeyNotFoundException($"Resource node entity {entityId} not found.");
    }

    public ref TownNpcEntity GetTownNpcRef(int entityId)
    {
        foreach (var chunk in _chunks.Values)
        {
            var span = chunk.TownNpcs;
            for (int i = 0; i < span.Length; i++)
                if (span[i].Id == entityId) return ref span[i];
        }
        throw new KeyNotFoundException($"Town NPC entity {entityId} not found.");
    }

    // ── Spatial queries ──────────────────────────────────────────────

    /// <summary>Builds a set of packed coordinates for all alive entities (players, monsters, NPCs, resource nodes).</summary>
    public void CollectEntitiesPositions(HashSet<long> set)
    {
        foreach (var p in _players)
            if (!p.IsDead) set.Add(Position.PackCoord(p.Position.X, p.Position.Y, p.Position.Z));
        foreach (var chunk in _chunks.Values)
            foreach (var m in chunk.AllSolidEntitiesWithHealth)
                if (!m.IsDead) set.Add(Position.PackCoord(m.Position.X, m.Position.Y, m.Position.Z));
    }

    /// <summary>Checks if any alive actor (player, monster, NPC) occupies the given position.</summary>
    public bool IsPositionOccupiedByEntity(Position pos)
    {
        foreach (var p in _players)
            if (!p.IsDead && p.Position == pos) return true;
        var c = Chunk.WorldToChunkCoord(pos);
        var chunk = TryGetChunk(c);
        if (chunk == null) return false;
        foreach (var m in chunk.AllSolidEntitiesWithHealth)
            if (!m.IsDead && m.Position == pos) return true;
        return false;
    }

    /// <summary>Convenience: get the chunk that contains the given world position.</summary>
    public Chunk? GetChunkForWorldPos(Position pos)
    {
        var c = Chunk.WorldToChunkCoord(pos);
        return TryGetChunk(c);
    }

    // ── Chunk migration (for moving entities) ────────────────────────

    /// <summary>Moves a monster to a new position, migrating between chunks if needed.</summary>
    public void MoveMonsterEntity(int entityId, Position from, Position to)
    {
        var oldC = Chunk.WorldToChunkCoord(from);
        var newC = Chunk.WorldToChunkCoord(to);

        var oldChunk = TryGetChunk(oldC);
        var newChunk = TryGetChunk(newC);

        Debug.Assert(oldChunk != null, $"Old chunk at {oldC} not found when moving monster entity {entityId}.");

        foreach (ref var monster in oldChunk.Monsters)
        {
            if (monster.Id == entityId)
            {
                monster.Position = to;
                if (oldChunk != newChunk)
                {
                    newChunk?.AddEntity(monster);
                    oldChunk.RemoveEntity(monster);
                }
                else
                {
                    oldChunk.MarkTileDirty(from);
                }
                return;
            }
        }

        Debug.Assert(false, $"Monster entity {entityId} not found in old chunk at {oldC} when moving.");
    }

    /// <summary>Moves a town NPC to a new position, migrating between chunks if needed.</summary>
    public void MoveNpcEntity(int entityId, Position from, Position to)
    {
        var oldC = Chunk.WorldToChunkCoord(from);
        var newC = Chunk.WorldToChunkCoord(to);

        var oldChunk = TryGetChunk(oldC);
        var newChunk = TryGetChunk(newC);

        Debug.Assert(oldChunk != null, $"Old chunk at {oldC} not found when moving NPC entity {entityId}.");

        foreach (ref var npc in oldChunk.TownNpcs)
        {
            if (npc.Id == entityId)
            {
                npc.Position = to;
                if (oldChunk != newChunk)
                {
                    newChunk?.AddEntity(npc);
                    oldChunk.RemoveEntity(npc);
                }
                return;
            }
        }

        Debug.Assert(false, $"NPC entity {entityId} not found in old chunk at {oldC} when moving.");
    }

    public bool ExistsChunk(ChunkPosition chunkPos, Generation.IDungeonGenerator generator)
    {
        long key = chunkPos.Pack();
        if (_chunks.ContainsKey(key))
            return true;
        if (_chunksDontExist.Contains(key))
            return false;
        var exists = generator.Exists(chunkPos);
        if (!exists)
            _chunksDontExist.Add(key);

        return exists;
    }

    public (Chunk Chunk, Generation.GenerationResult? NewlyGenerated) GetOrCreateChunk(ChunkPosition chunkPos, Generation.IDungeonGenerator generator)
    {
        long key = chunkPos.Pack();
        if (_chunks.TryGetValue(key, out var chunk))
            return (chunk, null);

        var result = generator.Generate(chunkPos);
        _chunks[key] = result.Chunk;
        ScanChunkForDynamicTiles(result.Chunk);
        return (result.Chunk, result);
    }


    public Chunk? TryGetChunk(ChunkPosition chunkPos)
    {
        long key = chunkPos.Pack();
        return _chunks.TryGetValue(key, out var chunk) ? chunk : null;
    }

    public Chunk GetChunk(ChunkPosition chunkPos)
    {
        long key = chunkPos.Pack();
        if (!_chunks.TryGetValue(key, out var chunk))
            throw new KeyNotFoundException($"Chunk at ({chunkPos.X}, {chunkPos.Y}, {chunkPos.Z}) not found.");
        return chunk;
    }

    public TileInfo GetTile(Position p)
    {
        var c = Chunk.WorldToChunkCoord(p);
        var chunk = TryGetChunk(c);
        if (chunk == null) return default;
        if (!chunk.WorldToLocal(p.X, p.Y, out var lx, out var ly)) return default;
        return chunk.Tiles[lx, ly];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsWalkable(Position pos)
    {
        return GetTile(pos).IsWalkable;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTransparent(Position pos)
    {
        return GetTile(pos).IsTransparent;
    }

    public void SetTile(Position pos, TileInfo tile)
    {
        var c = Chunk.WorldToChunkCoord(pos);
        var chunk = TryGetChunk(c);
        if (chunk == null) return;
        if (!chunk.WorldToLocal(pos.X, pos.Y, out var lx, out var ly)) return;

        bool wasDynamic = IsDynamicTile(chunk.Tiles[lx, ly]);
        bool isDynamic = IsDynamicTile(tile);

        chunk.Tiles[lx, ly] = tile;
        chunk.MarkTileDirty(pos);

        if (isDynamic && !wasDynamic)
            TrackDynamicTile(pos);
        else if (!isDynamic && wasDynamic)
            UntrackDynamicTile(pos);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPlaceable(Position pos, int placeableId, int extraData = 0)
    {
        var tile = GetTile(pos);
        tile.PlaceableItemId = placeableId;
        tile.PlaceableItemExtra = extraData;
        SetTile(pos, tile);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetTileChunkDirty(Position pos)
    {
        var c = Chunk.WorldToChunkCoord(pos);
        TryGetChunk(c)?.MarkModified();
    }


    /// <summary>Collects all dirty tile updates from loaded chunks and clears the dirty state.</summary>
    public void FlushDirtyTiles(List<(Position Pos, TileInfo Tile)> result)
    {
        foreach (var chunk in _chunks.Values)
            chunk.FlushDirtyTiles(result);
    }

    public IEnumerable<Chunk> LoadedChunks => _chunks.Values;

    /// <summary>Removes a chunk from the loaded set. Does not save — caller should save before calling.</summary>
    public void UnloadChunk(ChunkPosition chunkPos)
    {
        long key = chunkPos.Pack();
        _chunks.Remove(key);
        _dynamicTilesByChunk.Remove(key);
    }

    /// <summary>Returns all loaded chunks that have been modified since last save.</summary>
    public List<Chunk> GetModifiedChunks()
    {
        var result = new List<Chunk>();
        foreach (var chunk in _chunks.Values)
        {
            if (chunk.IsModifiedSinceLastSave)
                result.Add(chunk);
        }
        return result;
    }

    /// <summary>Adds a pre-built chunk to the loaded set (used when restoring from persistence).</summary>
    public void AddChunk(Chunk chunk)
    {
        long key = chunk.ChunkPosition.Pack();
        _chunks[key] = chunk;
        ScanChunkForDynamicTiles(chunk);
    }

    /// <summary>
    /// Opens a closed door at the given position. Sets the auto-close countdown in PlaceableItemExtra.
    /// </summary>
    public void OpenDoor(Position pos)
    {
        var tile = GetTile(pos);
        if (!IsDoorClosed(tile.PlaceableItemId, tile.PlaceableItemExtra)) return;
        tile.PlaceableItemExtra = DoorGraceTicks; // ticks until auto-close
        SetTile(pos, tile);
    }

    /// <summary>
    /// Per-tick update: processes all dynamic tiles (e.g. auto-closing doors).
    /// </summary>
    public void Update()
    {
        ProcessDynamicTiles();
    }

    /// <summary>Returns true if the given world position is tracked as a dynamic tile.</summary>
    public bool IsDynamicTileTracked(Position pos)
    {
        var c = Chunk.WorldToChunkCoord(pos);
        long chunkKey = c.Pack();
        if (!_dynamicTilesByChunk.TryGetValue(chunkKey, out var set)) return false;
        return set.Contains(pos.Pack());
    }

    /// <summary>Returns true if a tile has a stateful placeable in a non-default state (e.g. open door).</summary>
    private static bool IsDynamicTile(TileInfo tile)
    {
        if (tile.PlaceableItemId == 0) return false;
        var def = PlaceableDefinitions.Get(tile.PlaceableItemId);
        return def.HasState && tile.PlaceableItemExtra > 0;
    }

    private void TrackDynamicTile(Position pos)
    {
        var c = Chunk.WorldToChunkCoord(pos);
        long chunkKey = c.Pack();
        if (!_dynamicTilesByChunk.TryGetValue(chunkKey, out var set))
        {
            set = new HashSet<long>();
            _dynamicTilesByChunk[chunkKey] = set;
        }
        set.Add(pos.Pack());
    }

    private void UntrackDynamicTile(Position pos)
    {
        var c = Chunk.WorldToChunkCoord(pos);
        long chunkKey = c.Pack();
        if (_dynamicTilesByChunk.TryGetValue(chunkKey, out var set))
        {
            set.Remove(pos.Pack());
            if (set.Count == 0)
                _dynamicTilesByChunk.Remove(chunkKey);
        }
    }

    private void ScanChunkForDynamicTiles(Chunk chunk)
    {
        for (int lx = 0; lx < Chunk.Size; lx++)
            for (int ly = 0; ly < Chunk.Size; ly++)
                if (IsDynamicTile(chunk.Tiles[lx, ly]))
                    TrackDynamicTile(chunk.LocalToWorld(lx, ly));
    }

    private readonly List<(long ChunkKey, long TileKey)> _tmpTileKeys = new();
    private readonly HashSet<long> _tmpOccupiedPositions = new();

    private void ProcessDynamicTiles()
    {
        if (_dynamicTilesByChunk.Count == 0) return;

        // Build set of occupied positions for door blocking checks
        _tmpOccupiedPositions.Clear();
        foreach (var p in _players)
            if (!p.IsDead) _tmpOccupiedPositions.Add(Position.PackCoord(p.Position.X, p.Position.Y, p.Position.Z));
        foreach (var chunk in _chunks.Values)
        {
            foreach (var m in chunk.Monsters)
                if (!m.IsDead) _tmpOccupiedPositions.Add(Position.PackCoord(m.Position.X, m.Position.Y, m.Position.Z));
            foreach (var n in chunk.TownNpcs)
                if (!n.IsDead) _tmpOccupiedPositions.Add(Position.PackCoord(n.Position.X, n.Position.Y, n.Position.Z));
        }

        // Snapshot all tile keys to avoid modifying collections during iteration
        _tmpTileKeys.Clear();
        foreach (var (chunkKey, tileCoords) in _dynamicTilesByChunk)
            foreach (var tileKey in tileCoords)
                _tmpTileKeys.Add((chunkKey, tileKey));

        foreach (var (chunkKey, tileKey) in _tmpTileKeys)
        {
            var p = Position.UnpackCoord(tileKey);
            var c = Chunk.WorldToChunkCoord(p);
            var chunk = TryGetChunk(c);
            if (chunk == null) { UntrackDynamicTile(p); continue; }
            if (!chunk.WorldToLocal(p.X, p.Y, out var lx, out var ly)) { UntrackDynamicTile(p); continue; }

            ref var tile = ref chunk.Tiles[lx, ly];

            if (IsDoor(tile.PlaceableItemId) && tile.PlaceableItemExtra > 0)
            {
                int next = tile.PlaceableItemExtra - 1;
                if (next > 0)
                {
                    // Still counting down — quiet update (no network sync needed)
                    tile.PlaceableItemExtra = next;
                    chunk.MarkModified();
                }
                else if (!_tmpOccupiedPositions.Contains(tileKey))
                {
                    // Grace expired and unoccupied — close the door
                    tile.PlaceableItemExtra = 0;
                    chunk.MarkTileDirty(p);
                    UntrackDynamicTile(p);
                }
                else
                {
                    // Occupied — keep door open at minimum countdown
                    if (tile.PlaceableItemExtra != 1)
                    {
                        tile.PlaceableItemExtra = 1;
                        chunk.MarkModified();
                    }
                }
            }
            else
            {
                // No longer a dynamic tile
                UntrackDynamicTile(p);
            }
        }
    }
}
