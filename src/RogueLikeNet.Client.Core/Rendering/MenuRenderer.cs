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
    private static readonly string[] MainMenuItems = ["New Game/Load Game", "Play Online", "Admin Online", "Debug Mode:", "Help", "Quit"];
    public const int MainMenuPlayOfflineIndex = 0;
    public const int MainMenuPlayOnlineIndex = 1;
    public const int MainMenuAdminOnlineIndex = 2;
    public const int MainMenuDebugModeIndex = 3;
    public const int MainMenuHelpIndex = 4;
    public const int MainMenuQuitIndex = 5;


    private static readonly string[] PauseMenuItems = ["Resume", "Help", "Return to Main Menu"];
    public const int PauseMenuResumeIndex = 0;
    public const int PauseMenuHelpIndex = 1;
    public const int PauseMenuMainMenuIndex = 2;


    private static readonly string[] HelpLines =
    [
        "CONTROLS",
        "",
        "W / \u2191    Move / Attack up",
        "S / \u2193    Move / Attack down",
        "A / \u2190    Move / Attack left",
        "D / \u2192    Move / Attack right",
        "1-8      Use quick slot",
        "G        Pick up item",
        "X        Drop item",
        "P        Pickup placeable",
        "L        Look around",
        "E        Interact",
        "I        Inventory",
        "C        Crafting",
        "Escape   Ingame menu",
    ];

    public void RenderMainMenu(ISpriteRenderer r, int totalCols, int totalRows, int selectedIndex, bool debugEnabled)
    {
        r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        int boxW = 46;
        int boxH = 20;
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
            if (i == MainMenuDebugModeIndex)
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

        string footer = "\u2191\u2193 Navigate   Enter Select";
        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, footer, RenderingTheme.Dim);
    }

    public void RenderClassSelect(ISpriteRenderer r, int totalCols, int totalRows,
        int selectedClassIndex, string playerName, bool nameEditing, string nameEditText, bool canEditName)
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
        if (!nameEditing && canEditName)
            AsciiDraw.DrawCentered(r, totalCols, nameY + 1, "(T to edit name)", RenderingTheme.Dim);

        int cardStartY = nameY + 3;

        for (int i = 0; i < classCount; i++)
        {
            int cx = startX + i * (cardW + gap);
            bool selected = i == selectedClassIndex;
            var borderColor = selected ? RenderingTheme.ClassHighlight : RenderingTheme.ClassBorder;

            AsciiDraw.DrawBox(r, cx, cardStartY, cardW, cardH, borderColor);

            var classDef = ClassDefinitions.All[i];
            var stats = ClassDefinitions.GetStartingStats(i);

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
            : $"\u2190\u2192 Select Class {(canEditName ? " T Edit Name " : " ")} Enter Confirm   Esc Back";
        AsciiDraw.DrawCentered(r, totalCols, footerY, footer, RenderingTheme.Dim);
    }

    public void RenderNewGame(ISpriteRenderer r, int totalCols, int totalRows, int selectedIndex,
        string slotName, long worldSeed, int generatorIndex, bool seedEditing, string seedEditText,
        bool nameEditing, string nameEditText)
    {
        r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        int boxW = 46;
        int boxH = 16;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border, new Color4(10, 10, 15, 255));

        AsciiDraw.DrawCentered(r, totalCols, by + 1, "NEW GAME", RenderingTheme.Title);

        int sepY = by + 2;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            AsciiDraw.DrawChar(r, i, sepY, '\u2500', RenderingTheme.Dim);

        int itemStartY = sepY + 2;
        string nameDisplay = nameEditing ? nameEditText + "_" : (slotName.Length > 0 ? slotName : "(press Enter)");
        string seedDisplay = seedEditing ? seedEditText + "_" : worldSeed.ToString();
        string genName = GeneratorRegistry.GetName(generatorIndex);
        string[] labels =
        [
            "Name: " + nameDisplay,
            "Seed: " + seedDisplay,
            "Generator: \u25c4 " + genName + " \u25ba",
            "Randomize Seed",
            slotName.Length > 0 ? "Start" : "Start (name required)",
        ];

        int tx = bx + 6;
        for (int i = 0; i < labels.Length; i++)
        {
            bool sel = i == selectedIndex;
            string prefix = sel ? " \u25ba " : "   ";
            string label = prefix + labels[i];
            var color = sel ? RenderingTheme.Selected : RenderingTheme.Normal;
            if (i == 4 && slotName.Length == 0)
                color = sel ? RenderingTheme.Dim : RenderingTheme.Dim;
            AsciiDraw.DrawString(r, tx, itemStartY + i, label, color);
        }

        string footer;
        if (nameEditing)
            footer = "Type name   Enter Confirm   Esc Cancel";
        else if (seedEditing)
            footer = "Type seed   Enter Confirm   Esc Cancel";
        else if (selectedIndex == 1)
            footer = "\u2190\u2192 Adjust   Enter Edit   \u2191\u2193 Navigate";
        else if (selectedIndex == 2)
            footer = "\u2190\u2192 Change Generator   \u2191\u2193 Navigate";
        else
            footer = "\u2191\u2193 Navigate   Enter Select   Esc Back";
        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, footer, RenderingTheme.Dim);
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
        int debugLines = showDebug ? 11 : 0;
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
            AsciiDraw.DrawString(r, bx + 4, debugY, $"F  Free Craft: {(debug.FreeCrafting ? "ON" : "OFF")}", debug.FreeCrafting ? RenderingTheme.Selected : RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"Z  Toggle All", RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"+/-/0  Zoom: {debug.ZoomLevel}", RenderingTheme.Normal);
        }

        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, "\u2191\u2193 Navigate   Enter Select", RenderingTheme.Dim);
    }


    // ── Server Admin Screen ─────────────────────────────────

    public void RenderServerAdmin(ISpriteRenderer r, int totalCols, int totalRows,
        SaveSlotInfoMsg[] slots, string currentSlotId, int selectedIndex, string? statusMessage, bool isError,
        bool confirmingDelete, bool creatingNew, string newSlotName, bool waiting)
    {
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
        string[] actions = ["+ New Game", "Back"];
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

    public static string Truncate(string text, int maxLen)
        => text.Length <= maxLen ? text : text[..(maxLen - 2)] + "..";

    public static string FormatUnixMs(long unixMs)
    {
        if (unixMs <= 0) return "Never";
        var dt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
        return dt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    }
}
