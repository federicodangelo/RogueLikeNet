using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

/// <summary>
/// Wraps all network messages with a type discriminator for deserialization.
/// </summary>
[MessagePackObject]
public class NetworkEnvelope
{
    [Key(0)] public byte MessageType { get; set; }
    [Key(1)] public byte[] Payload { get; set; } = [];
}


public static class MessageTypes
{
    // Client → Server
    public const byte ClientInput = 1;
    public const byte LoginSend = 2;
    public const byte ChatSend = 3;
    public const byte ViewportInfo = 4;
    public const byte SaveGameCommand = 5;

    // Server → Client
    // 100 reserved (was WorldSnapshot, now unified into WorldDelta)
    public const byte WorldDelta = 101;
    public const byte ChatReceive = 103;
    public const byte PlayerSpawned = 104;
    public const byte EntityDied = 105;
    public const byte CombatEvent = 106;
    public const byte SaveGameResponse = 107;
}
