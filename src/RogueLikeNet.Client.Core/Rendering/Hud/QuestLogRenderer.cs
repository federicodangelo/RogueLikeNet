using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Rendering.Hud;

/// <summary>
/// Renders the quest log HUD panel (active quest list + detail view of the selected quest).
/// </summary>
public sealed class QuestLogRenderer
{
    private static readonly Color4 SelColor = new(255, 255, 80, 255);
    private static readonly Color4 ReadyColor = new(140, 255, 140, 255);
    private static readonly Color4 ProgressColor = new(180, 200, 255, 255);

    public void Render(ISpriteRenderer r, ActiveQuestInfoMsg[] quests, int selectedIndex, int hudStartCol, int totalRows)
    {
        HudPanelChrome.DrawBorder(r, hudStartCol, totalRows);

        int col = hudStartCol + 1;
        int innerW = AsciiDraw.HudColumns - 1;

        int row = 0;

        // Header
        HudPanelChrome.DrawHeader(r, col, row, innerW, "QUEST LOG");
        row++;
        AsciiDraw.DrawHudSeparator(r, col, row, innerW);
        row++;

        // Footer (bottom two rows)
        int footerRow = totalRows - 2;
        string hint = "[X] Abandon";
        if (hint.Length > innerW) hint = hint[..innerW];
        AsciiDraw.DrawString(r, col, footerRow, hint, RenderingTheme.Dim);
        AsciiDraw.DrawHudSeparator(r, col, footerRow - 1, innerW);

        int bottomLimit = footerRow - 1; // exclusive

        if (quests.Length == 0)
        {
            AsciiDraw.DrawString(r, col, row, "No active quests.", RenderingTheme.Dim);
            return;
        }

        // Split remaining vertical space: list on top, detail below.
        int available = bottomLimit - row;
        if (available <= 0) return;

        // Give the list at most half the space, but always show at least the selected area.
        int listRows = Math.Min(quests.Length, Math.Max(3, available / 2));
        if (listRows > available - 2) listRows = Math.Max(1, available - 2);

        // Clamp selection and compute scroll.
        if (selectedIndex < 0) selectedIndex = 0;
        if (selectedIndex >= quests.Length) selectedIndex = quests.Length - 1;

        int scrollOffset = 0;
        if (quests.Length > listRows)
        {
            scrollOffset = selectedIndex - listRows / 2;
            if (scrollOffset < 0) scrollOffset = 0;
            if (scrollOffset > quests.Length - listRows) scrollOffset = quests.Length - listRows;
        }

        int listEnd = Math.Min(scrollOffset + listRows, quests.Length);
        bool showTopArrow = scrollOffset > 0;
        bool showBottomArrow = listEnd < quests.Length;

        for (int i = scrollOffset; i < listEnd; i++)
        {
            bool sel = i == selectedIndex;
            var q = quests[i];
            bool complete = AllObjectivesComplete(q);
            string prefix = sel ? "\u25ba" : " ";
            string text = prefix + q.Title;
            if (text.Length > innerW - 1) text = text[..(innerW - 1)];
            Color4 color = sel ? SelColor : complete ? ReadyColor : ProgressColor;
            AsciiDraw.DrawString(r, col, row, text, color);

            if (i == scrollOffset && showTopArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2191', RenderingTheme.Dim);
            if (i == listEnd - 1 && showBottomArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2193', RenderingTheme.Dim);
            row++;
        }

        // Separator between list and detail
        if (row < bottomLimit)
        {
            AsciiDraw.DrawHudSeparator(r, col, row, innerW);
            row++;
        }

        // Detail pane
        if (selectedIndex >= 0 && selectedIndex < quests.Length && row < bottomLimit)
        {
            var q = quests[selectedIndex];
            string title = q.Title;
            if (title.Length > innerW) title = title[..innerW];
            AsciiDraw.DrawString(r, col, row++, title, RenderingTheme.Title);

            if (!string.IsNullOrEmpty(q.GiverName) && row < bottomLimit)
            {
                string giverLine = $"From: {q.GiverName}";
                if (giverLine.Length > innerW) giverLine = giverLine[..innerW];
                AsciiDraw.DrawString(r, col, row++, giverLine, RenderingTheme.Dim);
            }

            foreach (var obj in q.Objectives)
            {
                if (row >= bottomLimit) break;
                string mark = obj.Current >= obj.Target ? "[x] " : "[ ] ";
                string desc = string.IsNullOrEmpty(obj.Description)
                    ? $"{obj.Current}/{obj.Target}"
                    : $"{obj.Description} ({obj.Current}/{obj.Target})";
                string line = mark + desc;
                if (line.Length > innerW) line = line[..innerW];
                Color4 c = obj.Current >= obj.Target ? ReadyColor : RenderingTheme.Normal;
                AsciiDraw.DrawString(r, col, row++, line, c);
            }

            if (AllObjectivesComplete(q) && !string.IsNullOrEmpty(q.GiverName) && row < bottomLimit)
            {
                string returnLine = $"Return to {q.GiverName}!";
                if (returnLine.Length > innerW) returnLine = returnLine[..innerW];
                AsciiDraw.DrawString(r, col, row++, returnLine, ReadyColor);
            }
        }
    }

    public static bool AllObjectivesComplete(ActiveQuestInfoMsg q)
    {
        if (q.Objectives.Length == 0) return false;
        foreach (var o in q.Objectives)
            if (o.Current < o.Target) return false;
        return true;
    }
}
