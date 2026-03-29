using System.Diagnostics;
using Engine.Platform;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Client.Core.Systems;
using RogueLikeNet.Core.Utilities;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Renders performance stats and chat overlays.
/// </summary>
public sealed class OverlayRenderer
{
    public void RenderPerformance(ISpriteRenderer r, PerformanceMonitor perf, DebugSettings debug)
    {
        string fpsText = $"FPS:{perf.Fps} Tick:{perf.LatencyMs}ms";
        string inOutText = $"In:{perf.BandwidthInKBps:F1}KB/s Out:{perf.BandwidthOutKBps:F1}KB/s";
        int width = Math.Max(fpsText.Length, inOutText.Length) + 1;

        r.DrawRectScreen(0, 0, width * AsciiDraw.TileWidth, 4 * AsciiDraw.TileHeight, RenderingTheme.OverlayBg);

        var y = 0;
        AsciiDraw.DrawString(r, 0, y++, fpsText, RenderingTheme.Fps);
        AsciiDraw.DrawString(r, 0, y++, inOutText, RenderingTheme.Latency);

        if (debug.Enabled)
        {
            AsciiDraw.DrawString(r, 0, y++, "----------", RenderingTheme.Latency);
            var lastMeasurements = TimeMeasurerAccumulator.ThreadInstance.Value?.GetLastCompletedMeasurements();

            if (lastMeasurements != null)
            {
                foreach (var m in lastMeasurements)
                {
                    if (m.Hidden) continue; // Skip hidden measurements
                    var text = $"{new string('-', m.Depth)} {m.Name}:{m.Elapsed.TotalMilliseconds:F1}ms";
                    AsciiDraw.DrawString(r, 0, y++, text, RenderingTheme.Latency);
                }
            }
        }
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
