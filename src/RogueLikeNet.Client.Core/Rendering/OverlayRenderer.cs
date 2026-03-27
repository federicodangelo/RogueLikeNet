using Engine.Platform;
using RogueLikeNet.Client.Core.Systems;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Renders performance stats and chat overlays.
/// </summary>
public sealed class OverlayRenderer
{
    public void RenderPerformance(ISpriteRenderer r, PerformanceMonitor perf)
    {
        string fpsText = $"FPS:{perf.Fps}";
        string latText = $"Tick:{perf.LatencyMs}ms";
        string inText = $"In:{perf.BandwidthInKBps:F1}KB/s";
        string outText = $"Out:{perf.BandwidthOutKBps:F1}KB/s";
        int width = Math.Max(Math.Max(fpsText.Length, latText.Length),
                             Math.Max(inText.Length, outText.Length)) + 1;

        r.DrawRectScreen(0, 0, width * AsciiDraw.TileWidth, 4 * AsciiDraw.TileHeight, RenderingTheme.OverlayBg);

        AsciiDraw.DrawString(r, 0, 0, fpsText, RenderingTheme.Fps);
        AsciiDraw.DrawString(r, 0, 1, latText, RenderingTheme.Latency);
        AsciiDraw.DrawString(r, 0, 2, inText, RenderingTheme.Latency);
        AsciiDraw.DrawString(r, 0, 3, outText, RenderingTheme.Latency);
    }

    public void RenderChat(ISpriteRenderer r, int totalCols, int totalRows,
        ChatSystem chat)
    {
        int maxVisible = 5;
        int startY = totalRows - maxVisible - (chat.InputActive ? 1 : 0);
        int maxWidth = Math.Min(totalCols - 2, 60);

        if (chat.ChatLog.Count == 0 && !chat.InputActive) return;

        int msgCount = Math.Min(chat.ChatLog.Count, maxVisible);
        int bgHeight = msgCount + (chat.InputActive ? 1 : 0);
        if (bgHeight == 0) return;

        r.DrawRectScreen(0, startY * AsciiDraw.TileHeight,
            (maxWidth + 1) * AsciiDraw.TileWidth, bgHeight * AsciiDraw.TileHeight, RenderingTheme.ChatBg);

        for (int i = 0; i < msgCount; i++)
        {
            string msg = chat.ChatLog[chat.ChatLog.Count - msgCount + i];
            if (msg.Length > maxWidth) msg = msg[..maxWidth];
            AsciiDraw.DrawString(r, 0, startY + i, msg, RenderingTheme.ChatText);
        }

        if (chat.InputActive)
        {
            int inputY = totalRows - 1;
            string prompt = $"> {chat.InputText}_";
            if (prompt.Length > maxWidth) prompt = prompt[..maxWidth];
            AsciiDraw.DrawString(r, 0, inputY, prompt, RenderingTheme.ChatInput);
        }
    }
}
