using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Protocol.Tests;

public class NetworkEnvelopeTests
{
    [Fact]
    public void NetworkEnvelope_DefaultValues()
    {
        var msg = new NetworkEnvelope();
        Assert.Equal(0, msg.MessageType);
        Assert.Empty(msg.Payload);
        Assert.Equal(0, msg.IsCompressed);
    }

    [Fact]
    public void MessageTypes_ClientToServer_Constants()
    {
        Assert.Equal(1, MessageTypes.ClientInput);
        Assert.Equal(2, MessageTypes.LoginSend);
        Assert.Equal(3, MessageTypes.ChatSend);
    }

    [Fact]
    public void MessageTypes_ServerToClient_Constants()
    {
        Assert.Equal(100, MessageTypes.WorldSnapshot);
        Assert.Equal(101, MessageTypes.WorldDelta);
        Assert.Equal(103, MessageTypes.ChatReceive);
        Assert.Equal(104, MessageTypes.PlayerSpawned);
        Assert.Equal(105, MessageTypes.EntityDied);
        Assert.Equal(106, MessageTypes.CombatEvent);
    }
}
