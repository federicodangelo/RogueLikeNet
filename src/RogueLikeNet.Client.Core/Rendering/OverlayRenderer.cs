using System.Diagnostics;
using Engine.Platform;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Client.Core.Systems;
using RogueLikeNet.Core.Utilities;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Renders performance stats and chat overlays.
/// </summary>
public sealed class OverlayRenderer
{
    public void RenderPerformance(ISpriteRenderer r, int gameCols, PerformanceMonitor perf, DebugSettings debug)
    {
        string fpsText = $"FPS:{perf.Fps} Tick:{perf.LatencyMs}ms";
        string inOutText = $"In:{perf.BandwidthInKBps:F1}KB/s Out:{perf.BandwidthOutKBps:F1}KB/s";
        int width = Math.Max(fpsText.Length, inOutText.Length) + 1;

        // Collect debug measurement lines first so we can size the background correctly.
        List<string>? debugLines = null;
        if (debug.Enabled)
        {
            debugLines = new List<string> { "----------" };
            var lastMeasurements = TimeMeasurerAccumulator.ThreadInstance.Value?.GetLastCompletedMeasurements();
            if (lastMeasurements != null)
            {
                foreach (var m in lastMeasurements)
                {
                    if (m.Hidden) continue;
                    var text = $"{new string('-', m.Depth)} {m.Name}:{m.Elapsed.TotalMilliseconds:F1}ms";
                    debugLines.Add(text);
                    if (text.Length + 1 > width) width = text.Length + 1;
                }
            }
        }

        int height = 2 + (debugLines?.Count ?? 0);
        int startX = gameCols - width;
        if (startX < 0) startX = 0;

        r.DrawRectScreen(startX * AsciiDraw.TileWidth, 0,
            width * AsciiDraw.TileWidth, height * AsciiDraw.TileHeight, RenderingTheme.OverlayBg);

        var y = 0;
        AsciiDraw.DrawString(r, startX, y++, fpsText, RenderingTheme.Fps);
        AsciiDraw.DrawString(r, startX, y++, inOutText, RenderingTheme.Latency);

        if (debugLines != null)
        {
            foreach (var line in debugLines)
                AsciiDraw.DrawString(r, startX, y++, line, RenderingTheme.Latency);
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

    /// <summary>
    /// Renders a compact quest tracker in the top-left of the game viewport.
    /// Shows up to <paramref name="maxQuests"/> active quests with objective progress.
    /// Rendered before the HUD panel, so it lives inside the game area.
    /// </summary>
    public void RenderQuestTracker(ISpriteRenderer r, int gameCols, int totalRows,
        PlayerStateMsg? playerState, int maxQuests = 3)
    {
        if (playerState?.Quests == null) return;
        var quests = playerState.Quests.Active;
        if (quests == null || quests.Length == 0) return;

        int shown = Math.Min(maxQuests, quests.Length);

        // Compute required width from longest line.
        int width = "Quests:".Length;
        for (int i = 0; i < shown; i++)
        {
            var q = quests[i];
            if (q.Title.Length + 2 > width) width = q.Title.Length + 2;
            foreach (var o in q.Objectives)
            {
                int line = 6 + (string.IsNullOrEmpty(o.Description) ? 6 : o.Description.Length + 8);
                if (line > width) width = line;
            }
            if (AllComplete(q) && !string.IsNullOrEmpty(q.GiverName))
            {
                int line = 14 + q.GiverName.Length;
                if (line > width) width = line;
            }
        }
        width = Math.Min(width, 40);

        int rowCount = 1; // header
        for (int i = 0; i < shown; i++)
        {
            rowCount += 1 + quests[i].Objectives.Length;
            if (AllComplete(quests[i]) && !string.IsNullOrEmpty(quests[i].GiverName))
                rowCount++;
        }
        if (quests.Length > shown) rowCount++;

        int startX = 0;
        int startY = 0;

        r.DrawRectScreen(startX * AsciiDraw.TileWidth, startY * AsciiDraw.TileHeight,
            width * AsciiDraw.TileWidth, rowCount * AsciiDraw.TileHeight, RenderingTheme.OverlayBg);

        int y = startY;
        AsciiDraw.DrawString(r, startX, y++, "Quests:", RenderingTheme.Title);

        for (int i = 0; i < shown; i++)
        {
            var q = quests[i];
            bool complete = AllComplete(q);
            string title = q.Title.Length > width ? q.Title[..width] : q.Title;
            AsciiDraw.DrawString(r, startX, y++, title, complete ? RenderingTheme.Selected : RenderingTheme.Title);

            foreach (var o in q.Objectives)
            {
                string mark = o.Current >= o.Target ? "[x]" : "[ ]";
                string desc = string.IsNullOrEmpty(o.Description)
                    ? $"{o.Current}/{o.Target}"
                    : $"{o.Description} {o.Current}/{o.Target}";
                string line = $" {mark} {desc}";
                if (line.Length > width) line = line[..width];
                var color = o.Current >= o.Target ? RenderingTheme.Selected : RenderingTheme.Normal;
                AsciiDraw.DrawString(r, startX, y++, line, color);
            }

            if (complete && !string.IsNullOrEmpty(q.GiverName))
            {
                string returnLine = $" \u2192 Return to {q.GiverName}";
                if (returnLine.Length > width) returnLine = returnLine[..width];
                AsciiDraw.DrawString(r, startX, y++, returnLine, RenderingTheme.Selected);
            }
        }

        if (quests.Length > shown)
        {
            string more = $"+{quests.Length - shown} more...";
            if (more.Length > width) more = more[..width];
            AsciiDraw.DrawString(r, startX, y, more, RenderingTheme.Dim);
        }
    }

    private static bool AllComplete(ActiveQuestInfoMsg q)
    {
        if (q.Objectives.Length == 0) return false;
        foreach (var o in q.Objectives)
            if (o.Current < o.Target) return false;
        return true;
    }
}
