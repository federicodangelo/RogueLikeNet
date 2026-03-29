using System.Collections.Concurrent;
using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Server;

/// <summary>
/// Represents a connected player's server-side state.
/// </summary>
public class PlayerConnection
{
    public long ConnectionId { get; }
    public Arch.Core.Entity? PlayerEntity { get; set; }
    public string PlayerName { get; set; } = "";
    public long LastAckedTick { get; set; }
    public ConcurrentQueue<Protocol.Messages.ClientInputMsg> InputQueue { get; } = new();

    /// <summary>Tracks last-sent entity state per entity ID for delta compression.</summary>
    public Dictionary<long, EntityUpdateMsg> LastSentEntities { get; } = new();

    /// <summary>LRU tracker for chunks whose full static data has been sent.</summary>
    public ChunkTracker SentChunkTracker { get; } = new();

    /// <summary>Number of chunks visible in the client's viewport (sent by client).</summary>
    public int VisibleChunks { get; set; } = 9;

    /// <summary>Last serialized HUD bytes for delta compression.</summary>
    public byte[]? LastSentHudBytes { get; set; }

    private readonly Func<byte[], Task> _sendFunc;
    private long _bytesSent;
    private long _bytesReceived;

    public long BytesSent => Interlocked.Read(ref _bytesSent);
    public long BytesReceived => Interlocked.Read(ref _bytesReceived);

    public PlayerConnection(long connectionId, Func<byte[], Task> sendFunc)
    {
        ConnectionId = connectionId;
        _sendFunc = sendFunc;
    }

    public Task SendAsync(byte[] data)
    {
        Interlocked.Add(ref _bytesSent, data.Length);
        return _sendFunc(data);
    }

    public void TrackReceived(long bytes) => Interlocked.Add(ref _bytesReceived, bytes);
}
