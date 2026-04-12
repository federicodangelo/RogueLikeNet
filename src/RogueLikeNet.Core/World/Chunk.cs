using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Entities;

namespace RogueLikeNet.Core.World;

public record struct ChunkPosition(int X, int Y, int Z)
{
    public override readonly string ToString() => $"({X}, {Y}, {Z})";
    public const int DefaultZ = Position.DefaultZ;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChunkPosition FromCoords(int x, int y, int z) => new(x, y, z);

    public long Pack() => Position.PackCoord(X, Y, Z);
    public void Unpack(long packed)
    {
        var (x, y, z) = UnpackCoord(packed);
        X = x;
        Y = y;
        Z = z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long PackCoord(int x, int y, int z) => Position.PackCoord(x, y, z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long PackCoord(ChunkPosition pos) => PackCoord(pos.X, pos.Y, pos.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChunkPosition UnpackCoord(long packed)
    {
        var p = Position.UnpackCoord(packed);
        return FromCoords(p.X, p.Y, p.Z);
    }
}


public class Chunk
{
    public const int Size = 64;

    public ChunkPosition ChunkPosition { get; }
    public TileInfo[,] Tiles { get; }

    public int[,] LightLevels { get; }

    public byte[]? ClientExploredTiles { get; set; }

    public Dictionary<int, byte[]>? ServerExploredTilesByServerPlayerId { get; set; }

    // ── Entity storage ────────────────────────────────────────────────
    public Span<MonsterEntity> Monsters => CollectionsMarshal.AsSpan(_monsters);
    public Span<GroundItemEntity> GroundItems => CollectionsMarshal.AsSpan(_groundItems);
    public Span<ResourceNodeEntity> ResourceNodes => CollectionsMarshal.AsSpan(_resourceNodes);
    public Span<TownNpcEntity> TownNpcs => CollectionsMarshal.AsSpan(_townNpcs);
    public Span<CropEntity> Crops => CollectionsMarshal.AsSpan(_crops);
    public Span<AnimalEntity> Animals => CollectionsMarshal.AsSpan(_animals);

    // ── Light-emitting placeable tracking ─────────────────────────────
    public ReadOnlySpan<long> LightEmittingTiles => CollectionsMarshal.AsSpan(_lightEmittingTiles);

    public ref struct SolidEntityWithHealth
    {
        public readonly EntityRef Entity;
        public readonly ref Position Position;
        public readonly ref Health Health;
        public readonly bool IsDead => !Health.IsAlive;

        public SolidEntityWithHealth(EntityRef entityRef, ref Position position, ref Health health)
        {
            Entity = entityRef;
            Position = ref position;
            Health = ref health;
        }
    }

    public ref struct SolidEntitiesWithHealthEnumerator
    {
        private readonly Chunk _chunk;
        private int _phase; // 0 = monsters, 1 = resourceNodes, 2 = townNpcs
        private int _index;

        public SolidEntitiesWithHealthEnumerator(Chunk chunk)
        {
            _chunk = chunk;
            _phase = 0;
            _index = -1;
        }

        public SolidEntitiesWithHealthEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            _index++;
            while (_phase <= 3)
            {
                int count = _phase switch
                {
                    0 => _chunk._monsters.Count,
                    1 => _chunk._resourceNodes.Count,
                    2 => _chunk._townNpcs.Count,
                    3 => _chunk._animals.Count,
                    _ => 0
                };
                if (_index < count) return true;
                _phase++;
                _index = 0;
            }
            return false;
        }

        public SolidEntityWithHealth Current => _phase switch
        {
            0 => new SolidEntityWithHealth(
                new EntityRef(_chunk._monsters[_index].Id, EntityType.Monster),
                ref _chunk.Monsters[_index].Position,
                ref _chunk.Monsters[_index].Health),
            1 => new SolidEntityWithHealth(
                new EntityRef(_chunk._resourceNodes[_index].Id, EntityType.ResourceNode),
                ref _chunk.ResourceNodes[_index].Position,
                ref _chunk.ResourceNodes[_index].Health),
            2 => new SolidEntityWithHealth(
                new EntityRef(_chunk._townNpcs[_index].Id, EntityType.TownNpc),
                ref _chunk.TownNpcs[_index].Position,
                ref _chunk.TownNpcs[_index].Health),
            3 => new SolidEntityWithHealth(
                new EntityRef(_chunk._animals[_index].Id, EntityType.Animal),
                ref _chunk.Animals[_index].Position,
                ref _chunk.Animals[_index].Health),
            _ => throw new InvalidOperationException()
        };
    }

    public SolidEntitiesWithHealthEnumerator AllSolidEntitiesWithHealth =>
        new SolidEntitiesWithHealthEnumerator(this);

    private readonly List<MonsterEntity> _monsters = [];
    private readonly List<GroundItemEntity> _groundItems = [];
    private readonly List<ResourceNodeEntity> _resourceNodes = [];
    private readonly List<TownNpcEntity> _townNpcs = [];
    private readonly List<CropEntity> _crops = [];
    private readonly List<AnimalEntity> _animals = [];
    private readonly List<long> _lightEmittingTiles = [];

    /// <summary>World-coordinate dirty tiles modified since last flush.</summary>
    private readonly List<Position> _dirtyTiles = new();

    /// <summary>True if any tile has been modified since the last save.</summary>
    public bool IsModifiedSinceLastSave { get; private set; }

    public void MarkTileDirty(Position pos)
    {
        _dirtyTiles.Add(pos);
        MarkModified();
    }

    /// <summary>Marks the chunk as modified (e.g. entity added/removed) without a specific tile.</summary>
    public void MarkModified() => IsModifiedSinceLastSave = true;

    /// <summary>Clears the save-dirty flag after persisting.</summary>
    public void ClearSaveFlag() => IsModifiedSinceLastSave = false;

    public Chunk(ChunkPosition chunkPos)
    {
        ChunkPosition = chunkPos;
        Tiles = new TileInfo[Size, Size];
        LightLevels = new int[Size, Size];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InBounds(int localX, int localY)
            => localX >= 0 && localX < Size && localY >= 0 && localY < Size;

    /// <summary>
    /// Converts world coordinates to local chunk coordinates.
    /// Returns false if the world coords don't belong to this chunk.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool WorldToLocal(int worldX, int worldY, out int localX, out int localY)
    {
        WorldToLocal(worldX, worldY, ChunkPosition.X, ChunkPosition.Y, out localX, out localY);
        return InBounds(localX, localY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WorldToLocal(int worldX, int worldY, int chunkX, int chunkY, out int localX, out int localY)
    {
        localX = worldX - chunkX * Size;
        localY = worldY - chunkY * Size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Position LocalToWorld(int localX, int localY)
    {
        int worldX = ChunkPosition.X * Size + localX;
        int worldY = ChunkPosition.Y * Size + localY;
        return Position.FromCoords(worldX, worldY, ChunkPosition.Z);
    }

    /// <summary>
    /// Converts world coordinates to chunk coordinates.
    /// Z maps directly (each Z level = one chunk layer, no subdivision).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChunkPosition WorldToChunkCoord(Position world)
    {
        // Use integer division that floors towards negative infinity
        int cx = world.X >= 0 ? world.X / Size : (world.X - Size + 1) / Size;
        int cy = world.Y >= 0 ? world.Y / Size : (world.Y - Size + 1) / Size;
        return ChunkPosition.FromCoords(cx, cy, world.Z);
    }

    public void RemoveEntity(EntityRef entity)
    {
        switch (entity.Type)
        {
            case EntityType.Monster:
                _monsters.RemoveAll(m => m.Id == entity.Id);
                break;
            case EntityType.GroundItem:
                _groundItems.RemoveAll(i => i.Id == entity.Id);
                break;
            case EntityType.ResourceNode:
                _resourceNodes.RemoveAll(r => r.Id == entity.Id);
                break;
            case EntityType.TownNpc:
                _townNpcs.RemoveAll(n => n.Id == entity.Id);
                break;
            case EntityType.Crop:
                _crops.RemoveAll(c => c.Id == entity.Id);
                break;
            case EntityType.Animal:
                _animals.RemoveAll(a => a.Id == entity.Id);
                break;
        }
        MarkModified();
    }

    public void RemoveEntity(MonsterEntity entity) { _monsters.RemoveAll(m => m.Id == entity.Id); MarkModified(); }
    public void RemoveEntity(GroundItemEntity entity) { _groundItems.RemoveAll(i => i.Id == entity.Id); MarkModified(); }
    public void RemoveEntity(ResourceNodeEntity entity) { _resourceNodes.RemoveAll(r => r.Id == entity.Id); MarkModified(); }
    public void RemoveEntity(TownNpcEntity entity) { _townNpcs.RemoveAll(n => n.Id == entity.Id); MarkModified(); }
    public void RemoveEntity(CropEntity entity) { _crops.RemoveAll(c => c.Id == entity.Id); MarkModified(); }
    public void RemoveEntity(AnimalEntity entity) { _animals.RemoveAll(a => a.Id == entity.Id); MarkModified(); }

    public ref MonsterEntity AddEntity(MonsterEntity entity) { _monsters.Add(entity); MarkModified(); return ref Monsters[^1]; }
    public ref GroundItemEntity AddEntity(GroundItemEntity entity) { _groundItems.Add(entity); MarkModified(); return ref GroundItems[^1]; }
    public ref ResourceNodeEntity AddEntity(ResourceNodeEntity entity) { _resourceNodes.Add(entity); MarkModified(); return ref ResourceNodes[^1]; }
    public ref TownNpcEntity AddEntity(TownNpcEntity entity) { _townNpcs.Add(entity); MarkModified(); return ref TownNpcs[^1]; }
    public ref CropEntity AddEntity(CropEntity entity) { _crops.Add(entity); MarkModified(); return ref Crops[^1]; }
    public ref AnimalEntity AddEntity(AnimalEntity entity) { _animals.Add(entity); MarkModified(); return ref Animals[^1]; }

    // ── Ref getters for in-place mutation ─────────────────────────────

    public ref MonsterEntity GetMonsterRef(int entityId)
    {
        var span = Monsters;
        for (int i = 0; i < span.Length; i++)
            if (span[i].Id == entityId) return ref span[i];
        throw new KeyNotFoundException($"Monster entity {entityId} not found in chunk.");
    }

    public ref GroundItemEntity GetGroundItemRef(int entityId)
    {
        var span = GroundItems;
        for (int i = 0; i < span.Length; i++)
            if (span[i].Id == entityId) return ref span[i];
        throw new KeyNotFoundException($"Ground item entity {entityId} not found in chunk.");
    }

    public ref ResourceNodeEntity GetResourceNodeRef(int entityId)
    {
        var span = ResourceNodes;
        for (int i = 0; i < span.Length; i++)
            if (span[i].Id == entityId) return ref span[i];
        throw new KeyNotFoundException($"Resource node entity {entityId} not found in chunk.");
    }

    public ref TownNpcEntity GetTownNpcRef(int entityId)
    {
        var span = TownNpcs;
        for (int i = 0; i < span.Length; i++)
            if (span[i].Id == entityId) return ref span[i];
        throw new KeyNotFoundException($"Town NPC entity {entityId} not found in chunk.");
    }

    public ref CropEntity GetCropRef(int entityId)
    {
        var span = Crops;
        for (int i = 0; i < span.Length; i++)
            if (span[i].Id == entityId) return ref span[i];
        throw new KeyNotFoundException($"Crop entity {entityId} not found in chunk.");
    }

    public ref AnimalEntity GetAnimalRef(int entityId)
    {
        var span = Animals;
        for (int i = 0; i < span.Length; i++)
            if (span[i].Id == entityId) return ref span[i];
        throw new KeyNotFoundException($"Animal entity {entityId} not found in chunk.");
    }

    // Called by WorldMap when a chunk is loaded (from disk or generated) to perform any necessary initialization
    public void Init()
    {
        // Ensure light-emitting tile index is populated after loading (or generating) a chunk
        RebuildLightTileIndex();
    }

    public void ResetLight()
    {
        Array.Clear(LightLevels, 0, LightLevels.Length);
    }

    public void SetTile(int localX, int localY, TileInfo tile)
    {
        int oldPlaceableId = Tiles[localX, localY].PlaceableItemId;

        Tiles[localX, localY] = tile;
        MarkTileDirty(LocalToWorld(localX, localY));

        int newPlaceableId = tile.PlaceableItemId;
        if (oldPlaceableId != newPlaceableId)
        {
            var items = Data.GameData.Instance.Items;
            bool wasLight = oldPlaceableId != 0 && items.GetPlaceableLightRadius(oldPlaceableId) > 0;
            bool isLight = newPlaceableId != 0 && items.GetPlaceableLightRadius(newPlaceableId) > 0;
            if (isLight && !wasLight)
                TrackLightEmittingTile(localX, localY);
            else if (!isLight && wasLight)
                UntrackLightEmittingTile(localX, localY);
        }
    }

    private void RebuildLightTileIndex()
    {
        _lightEmittingTiles.Clear();
        var items = GameData.Instance.Items;
        for (int lx = 0; lx < Size; lx++)
            for (int ly = 0; ly < Size; ly++)
            {
                int pid = Tiles[lx, ly].PlaceableItemId;
                if (pid != 0 && items.GetPlaceableLightRadius(pid) > 0)
                    _lightEmittingTiles.Add(LocalToWorld(lx, ly).Pack());
            }
    }

    private void TrackLightEmittingTile(int localX, int localY)
    {
        int packed = localX * Size + localY;
        if (!_lightEmittingTiles.Contains(packed))
            _lightEmittingTiles.Add(packed);
    }

    private void UntrackLightEmittingTile(int localX, int localY)
    {
        _lightEmittingTiles.Remove(localX * Size + localY);
    }

    public void SetTileExploredByServerPlayerId(Position pos, int serverPlayerId)
    {
        if (!WorldToLocal(pos.X, pos.Y, out var lx, out var ly))
            return;

        ServerExploredTilesByServerPlayerId ??= new Dictionary<int, byte[]>();
        if (!ServerExploredTilesByServerPlayerId.TryGetValue(serverPlayerId, out var explored))
        {
            explored = new byte[Size * Size / 8];
            ServerExploredTilesByServerPlayerId[serverPlayerId] = explored;
        }

        if (ModifyTilesMask(explored, lx, ly))
            MarkModified();
    }

    public bool IsTileExploredByServerPlayerId(Position pos, int serverPlayerId)
    {
        if (ServerExploredTilesByServerPlayerId == null || !ServerExploredTilesByServerPlayerId.TryGetValue(serverPlayerId, out var explored))
            return false;

        if (!WorldToLocal(pos.X, pos.Y, out var lx, out var ly))
            return false;

        return IsTileExplored(explored, lx, ly);
    }

    public void SetTileExploredByClient(Position pos)
    {
        if (!WorldToLocal(pos.X, pos.Y, out var lx, out var ly))
            return;

        ClientExploredTiles ??= new byte[Size * Size / 8];

        if (ModifyTilesMask(ClientExploredTiles, lx, ly))
            MarkModified();
    }

    public bool IsTileExploredByClient(Position pos)
    {
        if (ClientExploredTiles == null)
            return false;

        if (!WorldToLocal(pos.X, pos.Y, out var lx, out var ly))
            return false;

        return IsTileExplored(ClientExploredTiles, lx, ly);
    }

    static private bool IsTileExplored(byte[] mask, int localX, int localY)
    {
        int bitIndex = localX + localY * Size;
        int byteIdx = bitIndex / 8;
        byte bitMask = (byte)(1 << (bitIndex % 8));
        return (mask[byteIdx] & bitMask) != 0;
    }

    static private bool ModifyTilesMask(byte[] mask, int localX, int localY)
    {
        int bitIndex = localX + localY * Size;
        int byteIdx = bitIndex / 8;
        byte bitMask = (byte)(1 << (bitIndex % 8));
        if ((mask[byteIdx] & bitMask) != 0)
            return false; // already set
        mask[byteIdx] |= bitMask;
        return true;
    }

    /// <summary>Removes dead entities from all lists (compacts in-place).</summary>
    public void RemoveDeadOrDestroyedEntities()
    {
        if (_monsters.RemoveAll(m => m.IsDead) != 0) MarkModified();
        if (_groundItems.RemoveAll(i => i.IsDestroyed) != 0) MarkModified();
        if (_resourceNodes.RemoveAll(r => r.IsDead) != 0) MarkModified();
        if (_townNpcs.RemoveAll(n => n.IsDead) != 0) MarkModified();
        if (_crops.RemoveAll(c => c.IsDestroyed) != 0) MarkModified();
        if (_animals.RemoveAll(a => a.IsDead) != 0) MarkModified();
    }

    /// <summary>Clears all entity lists (used when unloading a chunk).</summary>
    public void ClearEntities()
    {
        _monsters.Clear();
        _groundItems.Clear();
        _resourceNodes.Clear();
        _townNpcs.Clear();
        _crops.Clear();
        _animals.Clear();
    }

    internal void FlushDirtyTiles(List<(Position, TileInfo)> result)
    {
        if (_dirtyTiles.Count == 0) return;
        foreach (var pos in _dirtyTiles)
        {
            if (WorldToLocal(pos.X, pos.Y, out var localX, out var localY))
            {
                var tile = Tiles[localX, localY];
                result.Add((pos, tile));
            }
        }
        _dirtyTiles.Clear();
    }
}
