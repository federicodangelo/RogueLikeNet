using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Protocol.Tests;

public class MessageTests
{
    [Fact]
    public void LoginMsg_RoundTrip()
    {
        var msg = new LoginMsg
        {
            PlayerName = "TestHero",
            ClassId = 2
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<LoginMsg>(data);
        Assert.Equal("TestHero", result.PlayerName);
        Assert.Equal(2, result.ClassId);
    }

    [Fact]
    public void LoginMsg_DefaultValues()
    {
        var msg = new LoginMsg();
        Assert.Equal("", msg.PlayerName);
        Assert.Equal(0, msg.ClassId);
    }

    [Fact]
    public void LoginMsg_WrapUnwrap()
    {
        var msg = new LoginMsg { PlayerName = "Hero", ClassId = 1 };
        var payload = NetSerializer.Serialize(msg);
        var wrapped = NetSerializer.WrapMessage(MessageTypes.LoginSend, payload);
        var envelope = NetSerializer.UnwrapMessage(wrapped);
        Assert.Equal(MessageTypes.LoginSend, envelope.MessageType);
        var result = NetSerializer.Deserialize<LoginMsg>(envelope.Payload);
        Assert.Equal("Hero", result.PlayerName);
        Assert.Equal(1, result.ClassId);
    }

    [Fact]
    public void ChatMsg_RoundTrip()
    {
        var msg = new ChatMsg
        {
            SenderId = 1,
            SenderName = "Player1",
            Text = "Hello",
            Timestamp = 12345
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<ChatMsg>(data);
        Assert.Equal(1, result.SenderId);
        Assert.Equal("Player1", result.SenderName);
        Assert.Equal("Hello", result.Text);
        Assert.Equal(12345, result.Timestamp);
    }

    [Fact]
    public void ChatMsg_DefaultValues()
    {
        var msg = new ChatMsg();
        Assert.Equal(0, msg.SenderId);
        Assert.Equal("", msg.SenderName);
        Assert.Equal("", msg.Text);
        Assert.Equal(0, msg.Timestamp);
    }

    [Fact]
    public void ChatMsg_WrapUnwrap()
    {
        var msg = new ChatMsg { SenderId = 5, Text = "test" };
        var payload = NetSerializer.Serialize(msg);
        var wrapped = NetSerializer.WrapMessage(MessageTypes.ChatReceive, payload);
        var envelope = NetSerializer.UnwrapMessage(wrapped);
        Assert.Equal(MessageTypes.ChatReceive, envelope.MessageType);
        var result = NetSerializer.Deserialize<ChatMsg>(envelope.Payload);
        Assert.Equal(5, result.SenderId);
        Assert.Equal("test", result.Text);
    }
}
