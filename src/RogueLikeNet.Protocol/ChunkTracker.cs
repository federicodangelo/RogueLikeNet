namespace RogueLikeNet.Protocol;

/// <summary>
/// LRU-based chunk tracker that limits the number of tracked (sent) chunks.
/// When capacity is exceeded the least recently used chunks are evicted.
/// Capacity = max(9, visibleChunks * 2), giving room for the visible area
/// plus recently visited chunks.
/// </summary>
public sealed class ChunkTracker
{
    public const int MaxVisibleChunks = 600;
    public const int MinCapacity = 9;
    public const int MaxCapacity = MaxVisibleChunks * 2 * 3; // visible * 2 for buffer + extra for safety

    private readonly LinkedList<long> _lruOrder = new();
    private readonly Dictionary<long, LinkedListNode<long>> _map = new();
    private int capacity = MinCapacity * 2;

    public int Count => _map.Count;
    public int Capacity => capacity;

    /// <summary>Computes the LRU capacity from a visible chunk count.</summary>
    public static int ComputeCapacity(int visibleChunks)
    {
        return Math.Max(MinCapacity, Math.Min(visibleChunks, MaxVisibleChunks)) * 2 * 3;
    }

    /// <summary>
    /// Derives the minimum chunk range (distance in chunks around the player)
    /// needed to cover the given number of visible chunks.
    /// </summary>
    public static int ComputeChunkRange(int visibleChunks)
    {
        visibleChunks = Math.Clamp(visibleChunks, 1, MaxVisibleChunks);
        int side = (int)MathF.Ceiling(MathF.Sqrt(visibleChunks));
        return Math.Max(1, side / 2);
    }

    /// <summary>Updates the max capacity based on a visible chunk count.</summary>
    public void UpdateCapacity(int visibleChunks)
    {
        capacity = ComputeCapacity(visibleChunks);
    }

    /// <summary>Returns true if the chunk key is already tracked.</summary>
    public bool Contains(long key) => _map.ContainsKey(key);

    /// <summary>
    /// Marks a chunk key as recently used (adds if new, promotes if existing).
    /// Returns true if the key was newly added (not previously tracked).
    /// </summary>
    public bool Touch(long key)
    {
        if (_map.TryGetValue(key, out var node))
        {
            // Promote to most-recently-used
            _lruOrder.Remove(node);
            _lruOrder.AddLast(node);
            return false;
        }

        // New entry
        var newNode = _lruOrder.AddLast(key);
        _map[key] = newNode;
        return true;
    }

    /// <summary>
    /// Evicts least recently used entries until count &lt;= maxCapacity.
    /// Returns the keys of evicted chunks so the client can be notified.
    /// </summary>
    public long[] Evict()
    {
        if (_map.Count <= capacity)
            return [];

        int toEvict = _map.Count - capacity;
        var evicted = new long[toEvict];

        for (int i = 0; i < toEvict; i++)
        {
            var oldest = _lruOrder.First!;
            evicted[i] = oldest.Value;
            _lruOrder.Remove(oldest);
            _map.Remove(oldest.Value);
        }

        return evicted;
    }

    /// <summary>Returns all currently tracked chunk keys.</summary>
    public IEnumerable<long> GetTrackedKeys() => _map.Keys;

    /// <summary>Clears all tracked chunks.</summary>
    public void Clear()
    {
        _lruOrder.Clear();
        _map.Clear();
    }
}
