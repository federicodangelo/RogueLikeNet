using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Protocol.Tests;

public class ClientInputMsgTests
{
    [Fact]
    public void ClientInputMsg_DefaultValues()
    {
        var msg = new ClientInputMsg();
        Assert.Equal(0, msg.Tick);
        Assert.Equal(0, msg.ActionType);
        Assert.Equal(0, msg.TargetX);
        Assert.Equal(0, msg.TargetY);
        Assert.Equal(0, msg.ItemSlot);
    }

    [Fact]
    public void ClientInputMsg_RoundTrip()
    {
        var msg = new ClientInputMsg
        {
            Tick = 100,
            ActionType = 3,
            TargetX = -1,
            TargetY = 2,
            ItemSlot = 5
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<ClientInputMsg>(data);
        Assert.Equal(100, result.Tick);
        Assert.Equal(3, result.ActionType);
        Assert.Equal(-1, result.TargetX);
        Assert.Equal(2, result.TargetY);
        Assert.Equal(5, result.ItemSlot);
    }

    [Fact]
    public void ClientInputMsg_TargetSlot_DefaultValue()
    {
        var msg = new ClientInputMsg();
        Assert.Equal(0, msg.TargetSlot);
    }

    [Fact]
    public void ClientInputMsg_TargetSlot_RoundTrip()
    {
        var msg = new ClientInputMsg
        {
            Tick = 50,
            ActionType = 8, // SwapItems
            ItemSlot = 2,
            TargetSlot = 5
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<ClientInputMsg>(data);
        Assert.Equal(50, result.Tick);
        Assert.Equal(8, result.ActionType);
        Assert.Equal(2, result.ItemSlot);
        Assert.Equal(5, result.TargetSlot);
    }
}
