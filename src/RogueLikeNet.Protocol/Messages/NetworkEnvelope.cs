using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

/// <summary>
/// Wraps all network messages with a type discriminator for deserialization.
/// </summary>
[MessagePackObject]
public class NetworkEnvelope
{
    [Key(0)] public byte MessageType { get; set; }
    /// <summary>1 if Payload is deflate-compressed, 0 otherwise.</summary>
    [Key(1)] public byte IsCompressed { get; set; } = 0;
    [Key(2)] public byte[] Payload { get; set; } = [];
}   
 

public static class MessageTypes
{
    // Client → Server
    public const byte ClientInput = 1;
    public const byte AuthRequest = 2;
    public const byte ChatSend = 3;

    // Server → Client
    public const byte WorldSnapshot = 100;
    public const byte WorldDelta = 101;
    public const byte AuthResponse = 102;
    public const byte ChatReceive = 103;
    public const byte PlayerSpawned = 104;
    public const byte EntityDied = 105;
    public const byte CombatEvent = 106;
}
