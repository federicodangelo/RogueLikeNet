using System.Collections.Concurrent;

namespace RogueLikeNet.Server;

/// <summary>
/// Represents a connected player's server-side state.
/// </summary>
public class PlayerConnection
{
    public long ConnectionId { get; }
    public Arch.Core.Entity? PlayerEntity { get; set; }
    public long LastAckedTick { get; set; }
    public ConcurrentQueue<Protocol.Messages.ClientInputMsg> InputQueue { get; } = new();

    private readonly Func<byte[], Task> _sendFunc;

    public PlayerConnection(long connectionId, Func<byte[], Task> sendFunc)
    {
        ConnectionId = connectionId;
        _sendFunc = sendFunc;
    }

    public Task SendAsync(byte[] data) => _sendFunc(data);
}
