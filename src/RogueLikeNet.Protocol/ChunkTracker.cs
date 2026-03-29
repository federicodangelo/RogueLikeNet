namespace RogueLikeNet.Protocol;

/// <summary>
/// LRU-based chunk tracker that limits the number of tracked (sent) chunks.
/// When capacity is exceeded the least recently used chunks are evicted.
/// The max capacity is derived from the chunk range:
///   side = chunkRange * 2 + 1, capacity = side * side * 2
/// This gives enough room for the visible area plus some recently visited area.
/// </summary>
public sealed class ChunkTracker
{
    private readonly LinkedList<long> _lruOrder = new();
    private readonly Dictionary<long, LinkedListNode<long>> _map = new();
    private int _maxCapacity;

    public int Count => _map.Count;
    public int MaxCapacity => _maxCapacity;

    public ChunkTracker(int chunkRange = 1)
    {
        _maxCapacity = ComputeCapacity(chunkRange);
    }

    public static int ComputeCapacity(int chunkRange)
    {
        int side = chunkRange * 2 + 1;
        return side * side * 2;
    }

    /// <summary>Updates the max capacity based on a new chunk range.</summary>
    public void UpdateCapacity(int chunkRange)
    {
        _maxCapacity = ComputeCapacity(chunkRange);
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
        if (_map.Count <= _maxCapacity)
            return [];

        int toEvict = _map.Count - _maxCapacity;
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

    /// <summary>Clears all tracked chunks.</summary>
    public void Clear()
    {
        _lruOrder.Clear();
        _map.Clear();
    }
}
