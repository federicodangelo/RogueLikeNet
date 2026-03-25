using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Client.Core.State;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Renders the game world as an ASCII tile grid using the Engine's ISpriteRenderer.
/// Tile count adapts dynamically to window size. Uses the engine's bitmap font (CP437 8x16).
/// </summary>
public class TileRenderer
{
    public const int TileWidth = 9;   // 8px glyph + 1px advance
    public const int TileHeight = 16;
    public const int HudColumns = 20;
    private const float FontScale = 1f;

    // Colors used across menus and HUD
    private static readonly Color4 ColorBorder = new(180, 180, 180, 255);
    private static readonly Color4 ColorTitle = new(255, 200, 50, 255);
    private static readonly Color4 ColorNormal = new(180, 180, 180, 255);
    private static readonly Color4 ColorSelected = new(255, 255, 255, 255);
    private static readonly Color4 ColorDim = new(100, 100, 100, 255);
    private static readonly Color4 ColorHpBar = new(220, 50, 50, 255);
    private static readonly Color4 ColorHpFill = new(0, 200, 0, 255);
    private static readonly Color4 ColorHpText = new(255, 80, 80, 255);
    private static readonly Color4 ColorStats = new(200, 200, 200, 255);
    private static readonly Color4 ColorLevel = new(255, 255, 100, 255);
    private static readonly Color4 ColorItem = new(200, 180, 100, 255);
    private static readonly Color4 ColorSkillReady = new(100, 255, 100, 255);
    private static readonly Color4 ColorSkillCd = new(128, 128, 128, 255);
    private static readonly Color4 ColorInv = new(150, 200, 255, 255);
    private static readonly Color4 ColorOverlay = new(0, 0, 0, 160);
    private static readonly Color4 ColorFloor = new(150, 220, 130, 255);
    private static readonly Color4 ColorInvSel = new(255, 255, 80, 255);
    private static readonly Color4 ColorBlack = new(0, 0, 0, 255);
    private static readonly Color4 ColorOverlayBg = new(0, 0, 0, 180);
    private static readonly Color4 ColorFps = new(0, 255, 0, 255);
    private static readonly Color4 ColorLatency = new(255, 200, 50, 255);
    private static readonly Color4 ColorChatBg = new(0, 0, 0, 160);
    private static readonly Color4 ColorChatText = new(200, 200, 200, 255);
    private static readonly Color4 ColorChatInput = new(255, 255, 100, 255);

    private static readonly string[] MainMenuItems = ["Play Offline", "Play Online", "Help", "Quit"];
    private static readonly string[] PauseMenuItems = ["Resume", "Help", "Return to Main Menu"];

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
        "1-4      Use item slot",
        "Q        Use skill 1",
        "E        Use skill 2",
        "X        Drop item",
        "I        Inventory",
        "Escape   Pause / Back",
    ];

    private static readonly char[] Cp437 = CreateCp437Map();

    // ── Game Screen ────────────────────────────────────────────

    public void RenderGame(ISpriteRenderer r, ClientGameState state, int totalCols, int totalRows,
        float shakeX = 0, float shakeY = 0,
        bool inventoryMode = false, int inventoryIndex = 0)
    {
        r.DrawRectScreen(0, 0, totalCols * TileWidth, totalRows * TileHeight, ColorBlack);

        int gameCols = totalCols - HudColumns;
        RenderGameWorld(r, state, gameCols, totalRows, shakeX, shakeY);
        if (inventoryMode)
            RenderInventoryPanel(r, state, gameCols, totalRows, inventoryIndex);
        else
            RenderHudPanel(r, state, gameCols, totalRows);
    }

    private void RenderGameWorld(ISpriteRenderer r, ClientGameState state, int gameCols, int totalRows,
        float shakeX, float shakeY)
    {
        int cameraCenterX = state.PlayerX;
        int cameraCenterY = state.PlayerY;
        int halfW = gameCols / 2;
        int halfH = totalRows / 2;

        for (int sx = 0; sx < gameCols; sx++)
        for (int sy = 0; sy < totalRows; sy++)
        {
            int worldX = cameraCenterX - halfW + sx;
            int worldY = cameraCenterY - halfH + sy;
            var tile = state.GetTile(worldX, worldY);

            float px = sx * TileWidth + shakeX;
            float py = sy * TileHeight + shakeY;

            var bgColor = IntToColor4(tile.BgColor, tile.LightLevel);
            r.DrawRectScreen(px, py, TileWidth, TileHeight, bgColor);

            if (tile.GlyphId > 0 && tile.LightLevel > 0)
            {
                var fgColor = IntToColor4(tile.FgColor, tile.LightLevel);
                char ch = tile.GlyphId < 256 ? Cp437[tile.GlyphId] : '?';
                r.DrawTextScreen(px, py, ch.ToString(), fgColor, FontScale);
            }
        }

        // Entities
        foreach (var entity in state.Entities.Values)
        {
            int sx = entity.X - (cameraCenterX - halfW);
            int sy = entity.Y - (cameraCenterY - halfH);

            if (sx < 0 || sx >= gameCols || sy < 0 || sy >= totalRows) continue;

            float px = sx * TileWidth + shakeX;
            float py = sy * TileHeight + shakeY;

            var fgColor = IntToColor4(entity.FgColor, 10);
            char ch = entity.GlyphId < 256 ? Cp437[entity.GlyphId] : '?';
            r.DrawTextScreen(px, py, ch.ToString(), fgColor, FontScale);

            if (entity.MaxHealth > 0 && entity.Health < entity.MaxHealth)
            {
                float ratio = (float)entity.Health / entity.MaxHealth;
                r.DrawRectScreen(px, py - 2, TileWidth, 2, new Color4(255, 0, 0, 180));
                r.DrawRectScreen(px, py - 2, TileWidth * ratio, 2, new Color4(0, 255, 0, 180));
            }
        }
    }

    private void RenderHudPanel(ISpriteRenderer r, ClientGameState state, int hudStartCol, int totalRows)
    {
        float hx = hudStartCol * TileWidth;
        r.DrawRectScreen(hx, 0, HudColumns * TileWidth, totalRows * TileHeight, new Color4(15, 15, 20, 255));

        // Vertical separator
        DrawChar(r, hudStartCol, 0, '\u252C', ColorBorder);
        for (int y = 1; y < totalRows - 1; y++)
            DrawChar(r, hudStartCol, y, '\u2502', ColorBorder);
        DrawChar(r, hudStartCol, totalRows - 1, '\u2534', ColorBorder);

        int col = hudStartCol + 1;
        int innerW = HudColumns - 2;
        int row = 1;

        var hud = state.PlayerHud;
        if (hud == null)
        {
            DrawString(r, col, row, "No data", ColorDim);
            return;
        }

        // HP bar
        DrawString(r, col, row, "HP", ColorHpText);
        row++;
        int barW = innerW;
        float hpRatio = hud.MaxHealth > 0 ? (float)hud.Health / hud.MaxHealth : 0;
        int filled = (int)(barW * hpRatio);
        for (int i = 0; i < barW; i++)
            DrawChar(r, col + i, row, i < filled ? '\u2588' : '\u2591', i < filled ? ColorHpFill : ColorHpBar);
        row++;
        string hpText = $"{hud.Health}/{hud.MaxHealth}";
        DrawString(r, col, row, hpText, ColorHpText);
        row += 2;

        // Stats
        DrawString(r, col, row, $"ATK: {hud.Attack}", ColorStats);
        row++;
        DrawString(r, col, row, $"DEF: {hud.Defense}", ColorStats);
        row++;
        DrawString(r, col, row, $"Lv:  {hud.Level}", ColorLevel);
        row += 2;

        // Skills
        DrawString(r, col, row, "Skills", ColorTitle);
        row++;
        DrawHudSeparator(r, col, row, innerW);
        row++;
        for (int i = 0; i < Math.Min(hud.SkillIds.Length, 2); i++)
        {
            if (hud.SkillIds[i] == 0) continue;
            string key = i == 0 ? "Q" : "E";
            string name = i < hud.SkillNames.Length && !string.IsNullOrEmpty(hud.SkillNames[i])
                ? hud.SkillNames[i] : $"Skill {i + 1}";
            int cd = i < hud.SkillCooldowns.Length ? hud.SkillCooldowns[i] : 0;
            string text = cd > 0 ? $"[{key}]{name} cd:{cd}" : $"[{key}]{name}";
            DrawString(r, col, row, text, cd > 0 ? ColorSkillCd : ColorSkillReady);
            row++;
        }
        row++;

        // Equipment
        DrawString(r, col, row, "Equipment", ColorTitle);
        row++;
        DrawHudSeparator(r, col, row, innerW);
        row++;
        string wpn = !string.IsNullOrEmpty(hud.EquippedWeaponName) ? hud.EquippedWeaponName : "---";
        string arm = !string.IsNullOrEmpty(hud.EquippedArmorName) ? hud.EquippedArmorName : "---";
        DrawString(r, col, row, $"W: {wpn}", ColorItem);
        row++;
        DrawString(r, col, row, $"A: {arm}", ColorItem);
        row += 2;

        // Quick Use Slots
        DrawString(r, col, row, "Quick Use Slots", ColorTitle);
        row++;
        DrawHudSeparator(r, col, row, innerW);
        row++;
        int itemsToShow = Math.Min(hud.InventoryNames.Length, 4);
        for (int i = 0; i < 4; i++)
        {
            if (i < itemsToShow && !string.IsNullOrEmpty(hud.InventoryNames[i]))
            {
                int stack = i < hud.InventoryStackCounts.Length ? hud.InventoryStackCounts[i] : 1;
                string stackStr = stack > 1 ? $"x{stack}" : "";
                DrawString(r, col, row, $"[{i + 1}]{hud.InventoryNames[i]}{stackStr}", ColorItem);
            }
            else
                DrawString(r, col, row, $"[{i + 1}] ---", ColorDim);
            row++;
        }
        row++;

        DrawString(r, col, row, $"Inv:{hud.InventoryCount}/{hud.InventoryCapacity}", ColorInv);
        row += 2;

        // Floor items
        if (hud.FloorItemNames.Length > 0)
        {
            DrawString(r, col, row, "On Ground", ColorTitle);
            row++;
            DrawHudSeparator(r, col, row, innerW);
            row++;
            int floorToShow = Math.Min(hud.FloorItemNames.Length, 4);
            for (int i = 0; i < floorToShow; i++)
            {
                string name = hud.FloorItemNames[i];
                DrawString(r, col, row, $"  {name}", ColorFloor);
                row++;
            }
            row++;
            DrawString(r, col, row, "[G] Pick up", ColorDim);
            row++;
        }

        // Controls reminder at bottom
        int bottom = totalRows - 2;
        DrawString(r, col, bottom - 1, "[I] Inventory", ColorDim);
        DrawString(r, col, bottom, "[Esc] Menu", ColorDim);
    }

    private void RenderInventoryPanel(ISpriteRenderer r, ClientGameState state, int hudStartCol, int totalRows, int selectedIndex)
    {
        float hx = hudStartCol * TileWidth;
        r.DrawRectScreen(hx, 0, HudColumns * TileWidth, totalRows * TileHeight, new Color4(15, 15, 20, 255));

        // Vertical separator
        DrawChar(r, hudStartCol, 0, '\u252C', ColorBorder);
        for (int y = 1; y < totalRows - 1; y++)
            DrawChar(r, hudStartCol, y, '\u2502', ColorBorder);
        DrawChar(r, hudStartCol, totalRows - 1, '\u2534', ColorBorder);

        int col = hudStartCol + 1;
        int innerW = HudColumns - 2;
        int row = 1;

        DrawString(r, col, row, "INVENTORY", ColorTitle);
        row++;
        DrawHudSeparator(r, col, row, innerW);
        row++;

        var hud = state.PlayerHud;
        if (hud == null)
        {
            DrawString(r, col, row, "No data", ColorDim);
            return;
        }

        int cap = Math.Max(hud.InventoryCapacity, 4);
        for (int i = 0; i < cap; i++)
        {
            bool sel = i == selectedIndex;
            bool isQuickSlot = i < 4;
            string prefix = sel ? "\u25ba" : " ";
            string slotTag = isQuickSlot ? $"[{i + 1}]" : "   ";
            string name = i < hud.InventoryNames.Length && !string.IsNullOrEmpty(hud.InventoryNames[i])
                ? hud.InventoryNames[i]
                : "---";
            int stack = i < hud.InventoryStackCounts.Length ? hud.InventoryStackCounts[i] : 1;
            string stackStr = stack > 1 ? $" x{stack}" : "";
            string text = $"{prefix}{slotTag}{name}{stackStr}";
            var color = sel ? ColorInvSel : isQuickSlot ? ColorItem : ColorInv;
            DrawString(r, col, row, text, color);
            row++;
        }
        row++;

        // Equipped items section
        DrawString(r, col, row, "Equipped", ColorTitle);
        row++;
        DrawHudSeparator(r, col, row, innerW);
        row++;
        string eq_wpn = !string.IsNullOrEmpty(hud.EquippedWeaponName) ? hud.EquippedWeaponName : "---";
        string eq_arm = !string.IsNullOrEmpty(hud.EquippedArmorName) ? hud.EquippedArmorName : "---";
        DrawString(r, col, row, $"W: {eq_wpn}", ColorItem);
        row++;
        DrawString(r, col, row, $"A: {eq_arm}", ColorItem);
        row += 2;

        DrawString(r, col, row, $"Inv:{hud.InventoryCount}/{hud.InventoryCapacity}", ColorInv);
        row += 2;

        // Contextual actions
        DrawString(r, col, row, "Actions", ColorTitle);
        row++;
        DrawHudSeparator(r, col, row, innerW);
        row++;
        DrawString(r, col, row, "[Enter] Use item", ColorDim);
        row++;
        DrawString(r, col, row, "[E]     Equip", ColorDim);
        row++;
        DrawString(r, col, row, "[U]     Unequip wpn", ColorDim);
        row++;
        DrawString(r, col, row, "[R]     Unequip arm", ColorDim);
        row++;
        DrawString(r, col, row, "[X]     Drop item", ColorDim);
        row++;
        DrawString(r, col, row, "[Esc]   Close", ColorDim);
    }

    // ── Connecting Screen ──────────────────────────────────────

    public void RenderConnecting(ISpriteRenderer r, int totalCols, int totalRows, string? errorMessage = null)
    {
        r.DrawRectScreen(0, 0, totalCols * TileWidth, totalRows * TileHeight, ColorBlack);

        int boxW = 40;
        int boxH = errorMessage != null ? 10 : 7;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        DrawBox(r, bx, by, boxW, boxH, ColorBorder, new Color4(10, 10, 15, 255));

        if (errorMessage != null)
        {
            DrawCentered(r, totalCols, by + 2, "CONNECTION FAILED", ColorHpText);
            string msg = errorMessage.Length > boxW - 4 ? errorMessage[..(boxW - 4)] : errorMessage;
            DrawCentered(r, totalCols, by + 4, msg, ColorNormal);
            DrawCentered(r, totalCols, by + boxH - 2, "Press Enter to return", ColorDim);
        }
        else
        {
            DrawCentered(r, totalCols, by + 3, "Connecting...", ColorNormal);
        }
    }

    private static void DrawHudSeparator(ISpriteRenderer r, int col, int row, int width)
    {
        for (int i = 0; i < width; i++)
            DrawChar(r, col + i, row, '\u2500', ColorDim);
    }

    // ── Main Menu ──────────────────────────────────────────────

    public void RenderMainMenu(ISpriteRenderer r, int totalCols, int totalRows, int selectedIndex)
    {
        r.DrawRectScreen(0, 0, totalCols * TileWidth, totalRows * TileHeight, ColorBlack);

        int boxW = 40;
        int boxH = 18;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        DrawBox(r, bx, by, boxW, boxH, ColorBorder);

        // Title
        int titleY = by + 2;
        DrawCentered(r, totalCols, titleY, "R o g u e L i k e", ColorTitle);
        DrawCentered(r, totalCols, titleY + 1, ". N E T .", ColorTitle);

        // Decorative line
        int sepY = titleY + 3;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            DrawChar(r, i, sepY, '\u2500', ColorDim);

        // Menu items
        int itemStartY = sepY + 2;
        for (int i = 0; i < MainMenuItems.Length; i++)
        {
            bool sel = i == selectedIndex;
            string prefix = sel ? " \u25ba " : "   ";
            string text = prefix + MainMenuItems[i];
            int tx = bx + 6;
            DrawString(r, tx, itemStartY + i, text, sel ? ColorSelected : ColorNormal);
        }

        // Footer
        DrawCentered(r, totalCols, by + boxH - 2, "\u2191\u2193 Navigate   Enter Select", ColorDim);
    }

    // ── Help Screen ────────────────────────────────────────────

    public void RenderHelp(ISpriteRenderer r, int totalCols, int totalRows, bool isOverlay = false)
    {
        int boxW = 36;
        int boxH = HelpLines.Length + 6;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        if (isOverlay)
            FillOverlay(r, totalCols, totalRows);
        else
            r.DrawRectScreen(0, 0, totalCols * TileWidth, totalRows * TileHeight, ColorBlack);

        DrawBox(r, bx, by, boxW, boxH, ColorBorder, new Color4(10, 10, 15, 255));

        int row = by + 2;
        for (int i = 0; i < HelpLines.Length; i++)
        {
            var color = i == 0 ? ColorTitle : ColorNormal;
            if (string.IsNullOrEmpty(HelpLines[i]))
            {
                row++;
                continue;
            }
            DrawString(r, bx + 3, row, HelpLines[i], color);
            row++;
        }

        DrawCentered(r, totalCols, by + boxH - 2, "Press Esc to go back", ColorDim);
    }

    // ── Pause Menu ─────────────────────────────────────────────

    public void RenderPauseOverlay(ISpriteRenderer r, int totalCols, int totalRows, int selectedIndex)
    {
        FillOverlay(r, totalCols, totalRows);

        int boxW = 30;
        int boxH = PauseMenuItems.Length + 8;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        DrawBox(r, bx, by, boxW, boxH, ColorBorder, new Color4(10, 10, 15, 255));

        DrawCentered(r, totalCols, by + 2, "PAUSED", ColorTitle);

        int sepY = by + 3;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            DrawChar(r, i, sepY, '\u2500', ColorDim);

        int itemY = sepY + 2;
        for (int i = 0; i < PauseMenuItems.Length; i++)
        {
            bool sel = i == selectedIndex;
            string prefix = sel ? " \u25ba " : "   ";
            string text = prefix + PauseMenuItems[i];
            int tx = bx + 4;
            DrawString(r, tx, itemY + i, text, sel ? ColorSelected : ColorNormal);
        }

        DrawCentered(r, totalCols, by + boxH - 2, "\u2191\u2193 Navigate   Enter Select", ColorDim);
    }

    // ── Performance Overlay ──────────────────────────────────

    public void RenderPerformanceOverlay(ISpriteRenderer r, int fps, int latencyMs)
    {
        string fpsText = $"FPS:{fps}";
        string latText = $"Tick:{latencyMs}ms";
        int width = Math.Max(fpsText.Length, latText.Length) + 1;

        r.DrawRectScreen(0, 0, width * TileWidth, 2 * TileHeight, ColorOverlayBg);

        DrawString(r, 0, 0, fpsText, ColorFps);
        DrawString(r, 0, 1, latText, ColorLatency);
    }

    public void RenderChatOverlay(ISpriteRenderer r, int totalCols, int totalRows,
        List<string> chatLog, bool chatInputActive, string chatInputText)
    {
        int maxVisible = 5;
        int startY = totalRows - maxVisible - (chatInputActive ? 1 : 0);
        int maxWidth = Math.Min(totalCols - 2, 60);

        if (chatLog.Count == 0 && !chatInputActive) return;

        int msgCount = Math.Min(chatLog.Count, maxVisible);
        int bgHeight = msgCount + (chatInputActive ? 1 : 0);
        if (bgHeight == 0) return;

        r.DrawRectScreen(0, startY * TileHeight,
            (maxWidth + 1) * TileWidth, bgHeight * TileHeight, ColorChatBg);

        for (int i = 0; i < msgCount; i++)
        {
            string msg = chatLog[chatLog.Count - msgCount + i];
            if (msg.Length > maxWidth) msg = msg[..maxWidth];
            DrawString(r, 0, startY + i, msg, ColorChatText);
        }

        if (chatInputActive)
        {
            int inputY = totalRows - 1;
            string prompt = $"> {chatInputText}_";
            if (prompt.Length > maxWidth) prompt = prompt[..maxWidth];
            DrawString(r, 0, inputY, prompt, ColorChatInput);
        }
    }

    // ── Drawing Helpers ────────────────────────────────────────

    private static void DrawChar(ISpriteRenderer r, int tileX, int tileY, char ch, Color4 color)
    {
        float px = tileX * TileWidth;
        float py = tileY * TileHeight;
        r.DrawTextScreen(px, py, ch.ToString(), color, FontScale);
    }

    private static void DrawString(ISpriteRenderer r, int tileX, int tileY, string text, Color4 color)
    {
        float px = tileX * TileWidth;
        float py = tileY * TileHeight;
        r.DrawTextScreen(px, py, text, color, FontScale);
    }

    private static void DrawCentered(ISpriteRenderer r, int totalCols, int tileY, string text, Color4 color)
    {
        int tx = (totalCols - text.Length) / 2;
        DrawString(r, tx, tileY, text, color);
    }

    private static void DrawBox(ISpriteRenderer r, int x, int y, int w, int h, Color4 borderColor, Color4? fillColor = null)
    {
        if (fillColor.HasValue)
        {
            float fx = (x + 1) * TileWidth;
            float fy = (y + 1) * TileHeight;
            float fw = (w - 2) * TileWidth;
            float fh = (h - 2) * TileHeight;
            r.DrawRectScreen(fx, fy, fw, fh, fillColor.Value);
        }

        DrawChar(r, x, y, '\u250C', borderColor);
        DrawChar(r, x + w - 1, y, '\u2510', borderColor);
        DrawChar(r, x, y + h - 1, '\u2514', borderColor);
        DrawChar(r, x + w - 1, y + h - 1, '\u2518', borderColor);

        for (int i = 1; i < w - 1; i++)
        {
            DrawChar(r, x + i, y, '\u2500', borderColor);
            DrawChar(r, x + i, y + h - 1, '\u2500', borderColor);
        }
        for (int i = 1; i < h - 1; i++)
        {
            DrawChar(r, x, y + i, '\u2502', borderColor);
            DrawChar(r, x + w - 1, y + i, '\u2502', borderColor);
        }
    }

    private static void FillOverlay(ISpriteRenderer r, int totalCols, int totalRows)
    {
        r.DrawRectScreen(0, 0, totalCols * TileWidth, totalRows * TileHeight, ColorOverlay);
    }

    // ── Utilities ──────────────────────────────────────────────

    private static Color4 IntToColor4(int packedRgb, int lightLevel)
    {
        if (lightLevel <= 0) return ColorBlack;

        float brightness = lightLevel / 10f;
        byte cr = (byte)((packedRgb >> 16 & 0xFF) * brightness);
        byte cg = (byte)((packedRgb >> 8 & 0xFF) * brightness);
        byte cb = (byte)((packedRgb & 0xFF) * brightness);
        return new Color4(cr, cg, cb, 255);
    }

    private static char[] CreateCp437Map()
    {
        var map = new char[256];
        string cp437 = "\0\u263A\u263B\u2665\u2666\u2663\u2660\u2022\u25D8\u25CB\u25D9\u2642\u2640\u266A\u266B\u263C\u25BA\u25C4\u2195\u203C\u00B6\u00A7\u25AC\u21A8\u2191\u2193\u2192\u2190\u221F\u2194\u25B2\u25BC" +
                        " !\"#$%&'()*+,-./0123456789:;<=>?" +
                        "@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_" +
                        "`abcdefghijklmnopqrstuvwxyz{|}~\u2302" +
                        "\u00C7\u00FC\u00E9\u00E2\u00E4\u00E0\u00E5\u00E7\u00EA\u00EB\u00E8\u00EF\u00EE\u00EC\u00C4\u00C5\u00C9\u00E6\u00C6\u00F4\u00F6\u00F2\u00FB\u00F9\u00FF\u00D6\u00DC\u00A2\u00A3\u00A5\u20A7\u0192" +
                        "\u00E1\u00ED\u00F3\u00FA\u00F1\u00D1\u00AA\u00BA\u00BF\u2310\u00AC\u00BD\u00BC\u00A1\u00AB\u00BB\u2591\u2592\u2593\u2502\u2524\u2561\u2562\u2556\u2555\u2563\u2551\u2557\u255D\u255C\u255B\u2510" +
                        "\u2514\u2534\u252C\u251C\u2500\u253C\u255E\u255F\u255A\u2554\u2569\u2566\u2560\u2550\u256C\u2567\u2568\u2564\u2565\u2559\u2558\u2552\u2553\u256B\u256A\u2518\u250C\u2588\u2584\u258C\u2590\u2580" +
                        "\u03B1\u00DF\u0393\u03C0\u03A3\u03C3\u00B5\u03C4\u03A6\u0398\u03A9\u03B4\u221E\u03C6\u03B5\u2229\u2261\u00B1\u2265\u2264\u2320\u2321\u00F7\u2248\u00B0\u2219\u00B7\u221A\u207F\u00B2\u25A0\u00A0";

        for (int i = 0; i < Math.Min(cp437.Length, 256); i++)
            map[i] = cp437[i];

        return map;
    }
}
