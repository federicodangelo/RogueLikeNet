using RogueLikeNet.Protocol;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Protocol.Tests;

public class AuthMsgTests
{
    [Fact]
    public void AuthRequestMsg_RoundTrip()
    {
        var msg = new AuthRequestMsg
        {
            Username = "testuser",
            PasswordHash = "hash123"
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<AuthRequestMsg>(data);
        Assert.Equal("testuser", result.Username);
        Assert.Equal("hash123", result.PasswordHash);
    }

    [Fact]
    public void AuthResponseMsg_RoundTrip()
    {
        var msg = new AuthResponseMsg
        {
            Success = true,
            Message = "OK",
            PlayerId = 42,
            Token = "abc"
        };
        var data = NetSerializer.Serialize(msg);
        var result = NetSerializer.Deserialize<AuthResponseMsg>(data);
        Assert.True(result.Success);
        Assert.Equal("OK", result.Message);
        Assert.Equal(42, result.PlayerId);
        Assert.Equal("abc", result.Token);
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
    public void AuthRequestMsg_DefaultValues()
    {
        var msg = new AuthRequestMsg();
        Assert.Equal("", msg.Username);
        Assert.Equal("", msg.PasswordHash);
    }

    [Fact]
    public void AuthResponseMsg_DefaultValues()
    {
        var msg = new AuthResponseMsg();
        Assert.False(msg.Success);
        Assert.Equal("", msg.Message);
        Assert.Equal(0, msg.PlayerId);
        Assert.Equal("", msg.Token);
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
    public void AuthRequestMsg_WrapUnwrap()
    {
        var msg = new AuthRequestMsg { Username = "player1", PasswordHash = "secret" };
        var payload = NetSerializer.Serialize(msg);
        var wrapped = NetSerializer.WrapMessage(MessageTypes.AuthRequest, payload);
        var envelope = NetSerializer.UnwrapMessage(wrapped);
        Assert.Equal(MessageTypes.AuthRequest, envelope.MessageType);
        var result = NetSerializer.Deserialize<AuthRequestMsg>(envelope.Payload);
        Assert.Equal("player1", result.Username);
    }

    [Fact]
    public void AuthResponseMsg_WrapUnwrap()
    {
        var msg = new AuthResponseMsg { Success = true, PlayerId = 10, Token = "tok" };
        var payload = NetSerializer.Serialize(msg);
        var wrapped = NetSerializer.WrapMessage(MessageTypes.AuthResponse, payload);
        var envelope = NetSerializer.UnwrapMessage(wrapped);
        Assert.Equal(MessageTypes.AuthResponse, envelope.MessageType);
        var result = NetSerializer.Deserialize<AuthResponseMsg>(envelope.Payload);
        Assert.True(result.Success);
        Assert.Equal(10, result.PlayerId);
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
