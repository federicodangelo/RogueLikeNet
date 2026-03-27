using MessagePack;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Protocol;

/// <summary>
/// Serializes/deserializes network messages using MessagePack.
/// </summary>
public static class NetSerializer
{
    private static readonly MessagePackSerializerOptions Options =
        MessagePackSerializerOptions.Standard
            .WithSecurity(MessagePackSecurity.UntrustedData)
            .WithResolver(
                // Using these resolver we can be sure that non-AOT compatible formatters slip through (Nullable<structs> don't work in AOT contexts)
                MessagePack.Resolvers.CompositeResolver.Create(
                    MessagePack.Resolvers.BuiltinResolver.Instance,
                    MessagePack.Resolvers.SourceGeneratedFormatterResolver.Instance
                )
            );

    private static readonly MessagePackSerializerOptions OptionsWithCompression =
        Options
            .WithCompression(MessagePackCompression.Lz4BlockArray)
            .WithCompressionMinLength(CompressionThreshold)
;

    private const int CompressionThreshold = 4096;

    public static byte[] Serialize<T>(T message)
    {
        // Don't apply compression here, since WrapMessage will apply it to the whole envelope, which is more efficient than compressing individual messages (especially small ones)
        return MessagePackSerializer.Serialize(message, Options);
    }

    public static T Deserialize<T>(byte[] data)
        => MessagePackSerializer.Deserialize<T>(data, OptionsWithCompression);

    public static byte[] WrapMessage(byte messageType, byte[] payload)
    {
        byte[] finalPayload = payload;

        var envelope = new NetworkEnvelope
        {
            MessageType = messageType,
            Payload = finalPayload,
        };
        return MessagePackSerializer.Serialize(envelope, OptionsWithCompression);
    }

    public static NetworkEnvelope UnwrapMessage(byte[] data)
    {
        var envelope = Deserialize<NetworkEnvelope>(data);
        return envelope;
    }
}
