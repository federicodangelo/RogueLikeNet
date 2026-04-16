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


public class Chunk : EntitiesCollection
{
    public const int Size = 64;

    public ChunkPosition ChunkPosition { get; }
    public TileInfo[,] Tiles { get; }

    public int[,] LightLevels { get; }

    public byte[]? ClientExploredTiles { get; set; }

    public Dictionary<int, byte[]>? ServerExploredTilesByServerPlayerId { get; set; }

    // ── Light-emitting placeable tracking ─────────────────────────────
    public ReadOnlySpan<long> LightEmittingTiles => CollectionsMarshal.AsSpan(_lightEmittingTiles);

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

    protected override void OnModified()
    {
        MarkModified();
    }
}
