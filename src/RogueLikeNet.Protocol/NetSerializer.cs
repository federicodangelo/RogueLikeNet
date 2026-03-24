using MessagePack;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Protocol;

/// <summary>
/// Serializes/deserializes network messages using MessagePack.
/// </summary>
public static class NetSerializer
{
    private static readonly MessagePackSerializerOptions Options =
        MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);

    public static byte[] Serialize<T>(T message)
        => MessagePackSerializer.Serialize(message, Options);

    public static T Deserialize<T>(byte[] data)
        => MessagePackSerializer.Deserialize<T>(data, Options);

    public static byte[] WrapMessage(int messageType, byte[] payload)
    {
        var envelope = new NetworkEnvelope
        {
            MessageType = messageType,
            Payload = payload
        };
        return Serialize(envelope);
    }

    public static NetworkEnvelope UnwrapMessage(byte[] data)
        => Deserialize<NetworkEnvelope>(data);
}
