using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

/// <summary>
/// Wraps all network messages with a type discriminator for deserialization.
/// </summary>
[MessagePackObject]
public class NetworkEnvelope
{
    [Key(0)] public int MessageType { get; set; }
    [Key(1)] public byte[] Payload { get; set; } = [];
}

public static class MessageTypes
{
    // Client → Server
    public const int ClientInput = 1;
    public const int AuthRequest = 2;
    public const int ChatSend = 3;

    // Server → Client
    public const int WorldSnapshot = 100;
    public const int WorldDelta = 101;
    public const int AuthResponse = 102;
    public const int ChatReceive = 103;
    public const int PlayerSpawned = 104;
    public const int EntityDied = 105;
    public const int CombatEvent = 106;
}
