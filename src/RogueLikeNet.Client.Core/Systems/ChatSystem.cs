using System.Collections.Concurrent;
using Engine.Platform;
using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Systems;

/// <summary>
/// Manages chat input, message log, and network chat message buffering.
/// </summary>
public sealed class ChatSystem
{
    private readonly ConcurrentQueue<ChatMsg> _pendingChats = new();

    public List<string> ChatLog { get; } = new();
    public bool InputActive { get; set; }
    public string InputText { get; set; } = "";

    public void EnqueueMessage(ChatMsg msg)
    {
        _pendingChats.Enqueue(msg);
    }

    public void DrainPendingMessages()
    {
        while (_pendingChats.TryDequeue(out var chat))
        {
            AddChatLine($"{chat.SenderName}: {chat.Text}");
        }
    }

    public void AddChatLine(string line)
    {
        ChatLog.Add(line);
        if (ChatLog.Count > 50) ChatLog.RemoveAt(0);
    }

    public void HandleInput(IInputManager input, IGameServerConnection? connection)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            InputActive = false;
            InputText = "";
            return;
        }

        for (int i = 0; i < input.TextInputBackspacesCount; i++)
        {
            if (InputText.Length > 0)
                InputText = InputText[..^1];
        }

        if (input.TextInputReturnsCount > 0)
        {
            if (InputText.Length > 0 && connection != null)
                _ = connection.SendChatAsync(InputText);
            InputActive = false;
            InputText = "";
            return;
        }

        string typed = input.TextInput;
        if (typed.Length > 0 && InputText.Length < 100)
        {
            InputText += typed;
            if (InputText.Length > 100)
                InputText = InputText[..100];
        }
    }

    public void Clear()
    {
        ChatLog.Clear();
        InputActive = false;
        InputText = "";
        while (_pendingChats.TryDequeue(out _)) { }
    }
}
