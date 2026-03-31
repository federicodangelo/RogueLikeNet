using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Renders menu screens: main menu, class select, help, pause overlay, connecting screen.
/// </summary>
public sealed class MenuRenderer
{
    private static readonly string[] MainMenuItems = ["Play Offline", "Play Online", "Seed:", "Generator:", "Randomize Seed", "Debug Mode:", "Help", "Quit"];
    public const int MainMenuPlayOfflineIndex = 0;
    public const int MainMenuPlayOnlineIndex = 1;
    public const int MainMenuSeedIndex = 2;
    public const int MainMenuGeneratorIndex = 3;
    public const int MainMenuRandomizeSeedIndex = 4;
    public const int MainMenuDebugModeIndex = 5;
    public const int MainMenuHelpIndex = 6;
    public const int MainMenuQuitIndex = 7;


    private static readonly string[] PauseMenuItems = ["Resume", "Help", "Server Admin", "Return to Main Menu"];
    public const int PauseMenuResumeIndex = 0;
    public const int PauseMenuHelpIndex = 1;
    public const int PauseMenuServerAdminIndex = 2;
    public const int PauseMenuMainMenuIndex = 3;


    private static readonly string[] HelpLines =
    [
        "CONTROLS",
        "",
        "W / \u2191    Move up",
        "S / \u2193    Move down",
        "A / \u2190    Move left",
        "D / \u2192    Move right",
        "Space    Wait a turn",
        "F        Attack nearest",
        "G        Pick up item",
        "1-4      Use quick slot",
        "Q        Use skill 1",
        "E        Use skill 2",
        "X        Drop item",
        "I        Inventory",
        "Tab      Switch section",
        "Escape   Pause / Back",
        "",
        "INVENTORY",
        "1-4      Assign quick slot",
    ];

    public void RenderMainMenu(ISpriteRenderer r, int totalCols, int totalRows, int selectedIndex,
        long worldSeed, int generatorIndex, bool seedEditing, string seedEditText, bool debugEnabled)
    {
        r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        int boxW = 46;
        int boxH = 26;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border);

        int titleY = by + 2;
        AsciiDraw.DrawCentered(r, totalCols, titleY, "R o g u e L i k e", RenderingTheme.Title);
        AsciiDraw.DrawCentered(r, totalCols, titleY + 1, ". N E T .", RenderingTheme.Title);

        int sepY = titleY + 3;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            AsciiDraw.DrawChar(r, i, sepY, '\u2500', RenderingTheme.Dim);

        int itemStartY = sepY + 2;
        for (int i = 0; i < MainMenuItems.Length; i++)
        {
            bool sel = i == selectedIndex;
            string prefix = sel ? " \u25ba " : "   ";

            string label;
            if (i == 2)
            {
                string seedDisplay = seedEditing ? seedEditText + "_" : worldSeed.ToString();
                label = prefix + "Seed: " + seedDisplay;
            }
            else if (i == 3)
            {
                string genName = GeneratorRegistry.GetName(generatorIndex);
                label = prefix + "Generator: \u25c4 " + genName + " \u25ba";
            }
            else if (i == 5)
            {
                label = prefix + "Debug Mode: " + (debugEnabled ? "ON" : "OFF");
            }
            else
            {
                label = prefix + MainMenuItems[i];
            }

            int tx = bx + 6;
            AsciiDraw.DrawString(r, tx, itemStartY + i, label, sel ? RenderingTheme.Selected : RenderingTheme.Normal);
        }

        string footer = seedEditing
            ? "Type seed   Enter Confirm   Esc Cancel"
            : selectedIndex == 3
                ? "\u2190\u2192 Change Generator   \u2191\u2193 Navigate"
                : "\u2191\u2193 Navigate   Enter Select";
        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, footer, RenderingTheme.Dim);
    }

    public void RenderClassSelect(ISpriteRenderer r, int totalCols, int totalRows,
        int selectedClassIndex, string playerName, bool nameEditing, string nameEditText)
    {
        r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        int classCount = ClassDefinitions.NumClasses;
        int cardW = 18;
        int cardH = 18;
        int gap = 2;
        int totalW = classCount * cardW + (classCount - 1) * gap;
        int startX = (totalCols - totalW) / 2;

        AsciiDraw.DrawCentered(r, totalCols, 1, "SELECT YOUR CLASS", RenderingTheme.Title);

        int nameY = 3;
        string nameDisplay = nameEditing ? nameEditText + "_" : playerName;
        string nameLabel = $"Name: {nameDisplay}";
        AsciiDraw.DrawCentered(r, totalCols, nameY, nameLabel, nameEditing ? RenderingTheme.Selected : RenderingTheme.NameField);
        if (!nameEditing)
            AsciiDraw.DrawCentered(r, totalCols, nameY + 1, "(T to edit name)", RenderingTheme.Dim);

        int cardStartY = nameY + 3;

        for (int i = 0; i < classCount; i++)
        {
            int cx = startX + i * (cardW + gap);
            bool selected = i == selectedClassIndex;
            var borderColor = selected ? RenderingTheme.ClassHighlight : RenderingTheme.ClassBorder;

            AsciiDraw.DrawBox(r, cx, cardStartY, cardW, cardH, borderColor);

            var classDef = ClassDefinitions.All[i];
            var stats = classDef.StartingStats;

            int nameX = cx + (cardW - classDef.Name.Length) / 2;
            AsciiDraw.DrawString(r, nameX, cardStartY + 1, classDef.Name, selected ? RenderingTheme.ClassHighlight : RenderingTheme.Title);

            var art = ClassDefinitions.GetAsciiArt(i);
            for (int line = 0; line < art.Length; line++)
            {
                int artX = cx + (cardW - art[line].Length) / 2;
                AsciiDraw.DrawString(r, artX, cardStartY + 3 + line, art[line], selected ? RenderingTheme.Selected : RenderingTheme.Normal);
            }

            int statsY = cardStartY + 3 + art.Length + 1;
            AsciiDraw.DrawStatLine(r, cx + 2, statsY, "ATK", stats.Attack, cardW - 4);
            AsciiDraw.DrawStatLine(r, cx + 2, statsY + 1, "DEF", stats.Defense, cardW - 4);
            AsciiDraw.DrawStatLine(r, cx + 2, statsY + 2, "HP", stats.Health, cardW - 4);
            AsciiDraw.DrawStatLine(r, cx + 2, statsY + 3, "SPD", stats.Speed, cardW - 4);

            int skillY = statsY + 5;
            var skill0 = SkillDefinitions.Get(classDef.StartingSkill0);
            var skill1 = SkillDefinitions.Get(classDef.StartingSkill1);
            AsciiDraw.DrawString(r, cx + 2, skillY, skill0.Name, RenderingTheme.SkillName);
            AsciiDraw.DrawString(r, cx + 2, skillY + 1, skill1.Name, RenderingTheme.SkillName);

            if (selected)
            {
                int arrowY = cardStartY + cardH / 2;
                if (cx > 1)
                    AsciiDraw.DrawChar(r, cx - 1, arrowY, '\u25ba', RenderingTheme.ClassHighlight);
            }
        }

        int footerY = cardStartY + cardH + 1;
        string footer = nameEditing
            ? "Type name   Enter Confirm   Esc Cancel"
            : "\u2190\u2192 Select Class   T Edit Name   Enter Confirm   Esc Back";
        AsciiDraw.DrawCentered(r, totalCols, footerY, footer, RenderingTheme.Dim);
    }

    public void RenderConnecting(ISpriteRenderer r, int totalCols, int totalRows, string? errorMessage = null)
    {
        r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        int boxW = 50;
        int boxH = errorMessage != null ? 10 : 7;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border, new Color4(10, 10, 15, 255));

        if (errorMessage != null)
        {
            AsciiDraw.DrawCentered(r, totalCols, by + 2, "CONNECTION FAILED", RenderingTheme.HpText);
            string msg = errorMessage.Length > boxW - 4 ? errorMessage[..(boxW - 4)] : errorMessage;
            AsciiDraw.DrawCentered(r, totalCols, by + 4, msg, RenderingTheme.Normal);
            AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, "Press Enter to return", RenderingTheme.Dim);
        }
        else
        {
            AsciiDraw.DrawCentered(r, totalCols, by + 3, "Connecting...", RenderingTheme.Normal);
        }
    }

    public void RenderHelp(ISpriteRenderer r, int totalCols, int totalRows, bool isOverlay = false)
    {
        int boxW = 36;
        int boxH = HelpLines.Length + 6;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        if (isOverlay)
            AsciiDraw.FillOverlay(r, totalCols, totalRows);
        else
            r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border, new Color4(10, 10, 15, 255));

        int row = by + 2;
        for (int i = 0; i < HelpLines.Length; i++)
        {
            var color = i == 0 ? RenderingTheme.Title : RenderingTheme.Normal;
            if (string.IsNullOrEmpty(HelpLines[i]))
            {
                row++;
                continue;
            }
            AsciiDraw.DrawString(r, bx + 3, row, HelpLines[i], color);
            row++;
        }

        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, "Press Esc to go back", RenderingTheme.Dim);
    }

    public void RenderPauseOverlay(ISpriteRenderer r, int totalCols, int totalRows, int selectedIndex, DebugSettings? debug = null)
    {
        AsciiDraw.FillOverlay(r, totalCols, totalRows);

        bool showDebug = debug is { Enabled: true };
        int debugLines = showDebug ? 10 : 0;
        int boxW = 38;
        int boxH = PauseMenuItems.Length + 6 + debugLines;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border, new Color4(10, 10, 15, 255));

        AsciiDraw.DrawCentered(r, totalCols, by + 1, "PAUSED", RenderingTheme.Title);

        int sepY = by + 2;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            AsciiDraw.DrawChar(r, i, sepY, '\u2500', RenderingTheme.Dim);

        int itemY = sepY + 1;
        for (int i = 0; i < PauseMenuItems.Length; i++)
        {
            bool sel = i == selectedIndex;
            string prefix = sel ? " \u25ba " : "   ";
            string text = prefix + PauseMenuItems[i];
            int tx = bx + 4;
            AsciiDraw.DrawString(r, tx, itemY + i, text, sel ? RenderingTheme.Selected : RenderingTheme.Normal);
        }

        if (showDebug)
        {
            int debugY = itemY + PauseMenuItems.Length + 1;
            for (int i = bx + 2; i < bx + boxW - 2; i++)
                AsciiDraw.DrawChar(r, i, debugY, '\u2500', RenderingTheme.Dim);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, "DEBUG MODE", RenderingTheme.Title);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"V  Visibility: {(debug!.VisibilityOff ? "OFF" : "ON")}", debug.VisibilityOff ? RenderingTheme.Selected : RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"C  Collisions: {(debug.CollisionsOff ? "OFF" : "ON")}", debug.CollisionsOff ? RenderingTheme.Selected : RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"H  Invulnerable: {(debug.Invulnerable ? "ON" : "OFF")}", debug.Invulnerable ? RenderingTheme.Selected : RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"L  Lighting: {(debug.LightOff ? "OFF" : "ON")}", debug.LightOff ? RenderingTheme.Selected : RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"M  Max Speed: {(debug.MaxSpeed ? "ON" : "OFF")}", debug.MaxSpeed ? RenderingTheme.Selected : RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"Z  Toggle All", RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"+/-/0  Zoom: {debug.ZoomLevel}", RenderingTheme.Normal);
        }

        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, "\u2191\u2193 Navigate   Enter Select", RenderingTheme.Dim);
    }

    // ── Save Slot Screen ────────────────────────────────────

    public void RenderSaveSlotScreen(ISpriteRenderer r, int totalCols, int totalRows,
        SaveSlotInfoMsg[] slots, int selectedIndex, string? statusMessage, bool isError,
        bool confirmingDelete, bool creatingNew, string newSlotName, bool waiting)
    {
        r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        int boxW = 52;
        int slotLines = slots.Length * 3; // 2 lines per slot + 1 gap
        int boxH = Math.Max(14, slotLines + 12);
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border, new Color4(10, 10, 15, 255));

        AsciiDraw.DrawCentered(r, totalCols, by + 1, "SAVE SLOTS", RenderingTheme.Title);

        int sepY = by + 2;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            AsciiDraw.DrawChar(r, i, sepY, '\u2500', RenderingTheme.Dim);

        if (waiting)
        {
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2, "Loading...", RenderingTheme.Normal);
            return;
        }

        // Confirmation overlay
        if (confirmingDelete && selectedIndex < slots.Length)
        {
            var slot = slots[selectedIndex];
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 - 1, $"Delete \"{Truncate(slot.Name, 20)}\"?", RenderingTheme.Danger);
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 + 1, "Enter Confirm   Esc Cancel", RenderingTheme.Dim);
            return;
        }

        // New slot name editing overlay
        if (creatingNew)
        {
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 - 1, "Enter world name:", RenderingTheme.Normal);
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 + 1, newSlotName + "_", RenderingTheme.Selected);
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 + 3, "Enter Confirm   Esc Cancel", RenderingTheme.Dim);
            return;
        }

        int row = sepY + 2;
        for (int i = 0; i < slots.Length; i++)
        {
            bool sel = i == selectedIndex;
            var slot = slots[i];
            string prefix = sel ? " \u25ba " : "   ";

            string name = Truncate(slot.Name, boxW - 12);
            AsciiDraw.DrawString(r, bx + 4, row, prefix + name, sel ? RenderingTheme.Selected : RenderingTheme.SlotActive);

            string dateStr = FormatUnixMs(slot.LastSavedAtUnixMs);
            string genName = GeneratorRegistry.GetNameOrId(slot.GeneratorId);
            string details = $"     Seed: {slot.Seed}  Gen: {genName}  Saved: {dateStr}";
            AsciiDraw.DrawString(r, bx + 4, row + 1, Truncate(details, boxW - 6), RenderingTheme.SlotDate);

            row += 3;
        }

        // "New Game" action item
        {
            bool sel = selectedIndex == slots.Length;
            string prefix = sel ? " \u25ba " : "   ";
            AsciiDraw.DrawString(r, bx + 4, row, prefix + "+ New Game", sel ? RenderingTheme.Selected : RenderingTheme.Normal);
        }

        // Status message
        if (statusMessage != null)
        {
            var color = isError ? RenderingTheme.Danger : RenderingTheme.Floor;
            AsciiDraw.DrawCentered(r, totalCols, by + boxH - 4, Truncate(statusMessage, boxW - 4), color);
        }

        string footer = selectedIndex < slots.Length
            ? "\u2191\u2193 Navigate   Enter Load   X Delete   Esc Back"
            : "\u2191\u2193 Navigate   Enter Select   Esc Back";
        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, footer, RenderingTheme.Dim);
    }

    // ── Server Admin Screen ─────────────────────────────────

    public void RenderServerAdmin(ISpriteRenderer r, int totalCols, int totalRows,
        SaveSlotInfoMsg[] slots, string currentSlotId, int selectedIndex, string? statusMessage, bool isError,
        bool confirmingDelete, bool creatingNew, string newSlotName, bool waiting)
    {
        AsciiDraw.FillOverlay(r, totalCols, totalRows);

        int boxW = 54;
        int slotLines = slots.Length * 3;
        int boxH = Math.Max(16, slotLines + 14);
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border, new Color4(10, 10, 15, 255));

        AsciiDraw.DrawCentered(r, totalCols, by + 1, "SERVER ADMINISTRATION", RenderingTheme.Title);

        int sepY = by + 2;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            AsciiDraw.DrawChar(r, i, sepY, '\u2500', RenderingTheme.Dim);

        if (waiting)
        {
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2, "Loading...", RenderingTheme.Normal);
            return;
        }

        // Confirmation overlay
        if (confirmingDelete && selectedIndex < slots.Length)
        {
            var slot = slots[selectedIndex];
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 - 1, $"Delete \"{Truncate(slot.Name, 20)}\"?", RenderingTheme.Danger);
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 + 1, "Enter Confirm   Esc Cancel", RenderingTheme.Dim);
            return;
        }

        // New slot name editing overlay
        if (creatingNew)
        {
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 - 1, "Enter world name:", RenderingTheme.Normal);
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 + 1, newSlotName + "_", RenderingTheme.Selected);
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 + 3, "Enter Confirm   Esc Cancel", RenderingTheme.Dim);
            return;
        }

        int row = sepY + 2;
        for (int i = 0; i < slots.Length; i++)
        {
            bool sel = i == selectedIndex;
            var slot = slots[i];
            string prefix = sel ? " \u25ba " : "   ";

            bool isCurrent = slot.SlotId == currentSlotId;
            string marker = isCurrent ? " \u2713" : "";
            string name = Truncate(slot.Name, boxW - 14) + marker;
            var nameColor = isCurrent ? RenderingTheme.Floor : (sel ? RenderingTheme.Selected : RenderingTheme.SlotActive);
            AsciiDraw.DrawString(r, bx + 4, row, prefix + name, nameColor);

            string dateStr = FormatUnixMs(slot.LastSavedAtUnixMs);
            string genName = GeneratorRegistry.GetNameOrId(slot.GeneratorId);
            string details = $"     Seed: {slot.Seed}  Gen: {genName}  Saved: {dateStr}";
            AsciiDraw.DrawString(r, bx + 4, row + 1, Truncate(details, boxW - 6), RenderingTheme.SlotDate);

            row += 3;
        }

        // Action items
        int actionStart = row;
        string[] actions = ["+ New Game", "Save Current Game", "Back"];
        for (int i = 0; i < actions.Length; i++)
        {
            int idx = slots.Length + i;
            bool sel = idx == selectedIndex;
            string prefix = sel ? " \u25ba " : "   ";
            AsciiDraw.DrawString(r, bx + 4, actionStart + i, prefix + actions[i], sel ? RenderingTheme.Selected : RenderingTheme.Normal);
        }

        // Status message
        if (statusMessage != null)
        {
            var color = isError ? RenderingTheme.Danger : RenderingTheme.Floor;
            AsciiDraw.DrawCentered(r, totalCols, by + boxH - 4, Truncate(statusMessage, boxW - 4), color);
        }

        string footer = selectedIndex < slots.Length
            ? "\u2191\u2193 Navigate   Enter Load   X Delete   Esc Back"
            : "\u2191\u2193 Navigate   Enter Select   Esc Back";
        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, footer, RenderingTheme.Dim);
    }

    // ── Helpers ─────────────────────────────────────────────

    private static string Truncate(string text, int maxLen)
        => text.Length <= maxLen ? text : text[..(maxLen - 2)] + "..";

    private static string FormatUnixMs(long unixMs)
    {
        if (unixMs <= 0) return "Never";
        var dt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
        return dt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    }
}
