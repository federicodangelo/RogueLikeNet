using System.Runtime.CompilerServices;
using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.World;

public class Chunk
{
    public const int Size = 64;

    public int ChunkX { get; }
    public int ChunkY { get; }
    public int ChunkZ { get; }
    public TileInfo[,] Tiles { get; }

    public int[,] LightLevels { get; }

    // ── Entity storage ────────────────────────────────────────────────
    public IEnumerable<MonsterEntity> Monsters => _monsters;
    public IEnumerable<GroundItemEntity> GroundItems => _groundItems;
    public IEnumerable<ResourceNodeEntity> ResourceNodes => _resourceNodes;
    public IEnumerable<TownNpcEntity> TownNpcs => _townNpcs;
    public IEnumerable<ElementEntity> Elements => _elements;

    private readonly List<MonsterEntity> _monsters = [];
    private readonly List<GroundItemEntity> _groundItems = [];
    private readonly List<ResourceNodeEntity> _resourceNodes = [];
    private readonly List<TownNpcEntity> _townNpcs = [];
    private readonly List<ElementEntity> _elements = [];

    /// <summary>World-coordinate dirty tiles modified since last flush.</summary>
    private readonly List<(int WorldX, int WorldY, int WorldZ)> _dirtyTiles = new();

    public IReadOnlyList<(int WorldX, int WorldY, int WorldZ)> DirtyTiles => _dirtyTiles;

    /// <summary>True if any tile has been modified since the last save.</summary>
    public bool IsModifiedSinceLastSave { get; private set; }

    public void MarkTileDirty(int worldX, int worldY, int worldZ)
    {
        _dirtyTiles.Add((worldX, worldY, worldZ));
        IsModifiedSinceLastSave = true;
    }

    public void ClearDirtyTiles() => _dirtyTiles.Clear();

    /// <summary>Marks the chunk as modified (e.g. entity added/removed) without a specific tile.</summary>
    public void MarkModified() => IsModifiedSinceLastSave = true;

    /// <summary>Clears the save-dirty flag after persisting.</summary>
    public void ClearSaveFlag() => IsModifiedSinceLastSave = false;

    public Chunk(int chunkX, int chunkY, int chunkZ)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        ChunkZ = chunkZ;
        Tiles = new TileInfo[Size, Size];
        LightLevels = new int[Size, Size];
    }

    public ref TileInfo GetTile(int localX, int localY) => ref Tiles[localX, localY];

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
        localX = worldX - ChunkX * Size;
        localY = worldY - ChunkY * Size;
        return InBounds(localX, localY);
    }

    /// <summary>
    /// Converts world coordinates to chunk coordinates.
    /// Z maps directly (each Z level = one chunk layer, no subdivision).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int ChunkX, int ChunkY, int ChunkZ) WorldToChunkCoord(int worldX, int worldY, int worldZ)
    {
        // Use integer division that floors towards negative infinity
        int cx = worldX >= 0 ? worldX / Size : (worldX - Size + 1) / Size;
        int cy = worldY >= 0 ? worldY / Size : (worldY - Size + 1) / Size;
        return (cx, cy, worldZ);
    }

    public void RemoveEntity(Entity entity)
    {
        switch (entity)
        {
            case MonsterEntity m:
                _monsters.Remove(m);
                break;
            case GroundItemEntity i:
                _groundItems.Remove(i);
                break;
            case ResourceNodeEntity r:
                _resourceNodes.Remove(r);
                break;
            case TownNpcEntity n:
                _townNpcs.Remove(n);
                break;
            case ElementEntity e:
                _elements.Remove(e);
                break;
        }
        MarkModified();
    }

    public void AddEntity(Entity entity)
    {
        switch (entity)
        {
            case MonsterEntity m:
                _monsters.Add(m);
                break;
            case GroundItemEntity i:
                _groundItems.Add(i);
                break;
            case ResourceNodeEntity r:
                _resourceNodes.Add(r);
                break;
            case TownNpcEntity n:
                _townNpcs.Add(n);
                break;
            case ElementEntity e:
                _elements.Add(e);
                break;
        }
        MarkModified();
    }

    public void ResetLight()
    {
        Array.Clear(LightLevels, 0, LightLevels.Length);
    }

    /// <summary>Removes dead entities from all lists (compacts in-place).</summary>
    public void RemoveDeadEntities()
    {
        _monsters.RemoveAll(m => m.IsDead);
        _groundItems.RemoveAll(i => i.IsDead);
        _resourceNodes.RemoveAll(r => r.IsDead);
        _townNpcs.RemoveAll(n => n.IsDead);
    }

    /// <summary>Clears all entity lists (used when unloading a chunk).</summary>
    public void ClearEntities()
    {
        _monsters.Clear();
        _groundItems.Clear();
        _resourceNodes.Clear();
        _townNpcs.Clear();
        _elements.Clear();
    }
}
