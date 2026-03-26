using System.IO.Compression;
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

    private const int CompressionThreshold = 4096;

    public static byte[] Serialize<T>(T message)
        => MessagePackSerializer.Serialize(message, Options);

    public static T Deserialize<T>(byte[] data)
        => MessagePackSerializer.Deserialize<T>(data, Options);

    public static byte[] WrapMessage(byte messageType, byte[] payload)
    {
        byte isCompressed = 0;
        byte[] finalPayload = payload;

        if (payload.Length > CompressionThreshold)
        {
            finalPayload = Compress(payload);
            isCompressed = 1;
        }

        var envelope = new NetworkEnvelope
        {
            MessageType = messageType,
            Payload = finalPayload,
            IsCompressed = isCompressed
        };
        return Serialize(envelope);
    }

    public static NetworkEnvelope UnwrapMessage(byte[] data)
    {
        var envelope = Deserialize<NetworkEnvelope>(data);
        if (envelope.IsCompressed != 0)
        {
            envelope.Payload = Decompress(envelope.Payload);
            envelope.IsCompressed = 0;
        }
        return envelope;
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
            deflate.Write(data, 0, data.Length);
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }
}
