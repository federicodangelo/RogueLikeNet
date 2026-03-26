using Engine.Core;
using Engine.Platform;
using Engine.Rendering.Base;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Renders the game world as an ASCII tile grid using the Engine's ISpriteRenderer.
/// Tile count adapts dynamically to window size. Uses the engine's bitmap font (CP437 8x16).
/// </summary>
public class TileRenderer
{
    public const int TileWidth = (int)(9 * FontScale);  // (8px glyph + 1px advance) * 2
    public const int TileHeight = (int)(16 * FontScale);
    public const int HudColumns = 30;
    private const float FontScale = 1.5f;

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

    private static readonly string[] MainMenuItems = ["Play Offline", "Play Online", "Seed:", "Randomize Seed", "Help", "Quit"];
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

    private static readonly char[] Cp437 = MiniBitmapFont.Cp437ToUnicode;

    // ── Game Screen ────────────────────────────────────────────

    public void RenderGame(ISpriteRenderer r, ClientGameState state, int totalCols, int totalRows,
        float shakeX = 0, float shakeY = 0,
        bool inventoryMode = false, HudLayout? layout = null)
    {
        r.DrawRectScreen(0, 0, totalCols * TileWidth, totalRows * TileHeight, ColorBlack);

        int gameCols = totalCols - HudColumns;
        RenderGameWorld(r, state, gameCols, totalRows, shakeX, shakeY);
        if (inventoryMode)
            RenderInventoryPanel(r, state, gameCols, totalRows, layout);
        else
            RenderHudPanel(r, state, gameCols, totalRows, layout);
    }

    private void RenderGameWorld(ISpriteRenderer r, ClientGameState state, int gameCols, int totalRows,
        float shakeX, float shakeY)
    {
        int cameraCenterX = state.PlayerX;
        int cameraCenterY = state.PlayerY;
        int halfW = gameCols / 2;
        int halfH = totalRows / 2;

        // Pass 1: tile backgrounds and foreground glyphs
        for (int sx = 0; sx < gameCols; sx++)
            for (int sy = 0; sy < totalRows; sy++)
            {
                int worldX = cameraCenterX - halfW + sx;
                int worldY = cameraCenterY - halfH + sy;
                var tile = state.GetTile(worldX, worldY);

                float px = sx * TileWidth + shakeX;
                float py = sy * TileHeight + shakeY;

                bool visible = state.IsVisible(worldX, worldY);
                bool explored = state.IsExplored(worldX, worldY);

                if (visible)
                {
                    // Currently in FOV — render with actual light level
                    var bgColor = IntToColor4(tile.BgColor, tile.LightLevel);
                    r.DrawRectScreen(px, py, TileWidth, TileHeight, bgColor);

                    if (tile.GlyphId > 0 && tile.LightLevel > 0)
                    {
                        var fgColor = IntToColor4(tile.FgColor, tile.LightLevel);
                        char ch = tile.GlyphId < 256 ? Cp437[tile.GlyphId] : '?';
                        r.DrawTextScreen(px, py, ch.ToString(), fgColor, FontScale);
                    }
                }
                else if (explored && tile.GlyphId > 0)
                {
                    // Explored but not in FOV — dim fog of war
                    var bgColor = FogColor(tile.BgColor);
                    r.DrawRectScreen(px, py, TileWidth, TileHeight, bgColor);

                    var fgColor = FogColor(tile.FgColor);
                    char ch = tile.GlyphId < 256 ? Cp437[tile.GlyphId] : '?';
                    r.DrawTextScreen(px, py, ch.ToString(), fgColor, FontScale);
                }
                else
                {
                    // Unknown — black
                    r.DrawRectScreen(px, py, TileWidth, TileHeight, ColorBlack);
                }
            }

        // Pass 2: glow effects behind torches and light-emitting tiles (visible only)
        for (int sx = 0; sx < gameCols; sx++)
            for (int sy = 0; sy < totalRows; sy++)
            {
                int worldX = cameraCenterX - halfW + sx;
                int worldY = cameraCenterY - halfH + sy;

                if (!state.IsVisible(worldX, worldY)) continue;

                var tile = state.GetTile(worldX, worldY);

                if (tile.LightLevel < 5) continue;

                // Glow behind torches
                if (tile.GlyphId == TileDefinitions.GlyphTorch)
                {
                    float cx = sx * TileWidth + TileWidth * 0.5f + shakeX;
                    float cy = sy * TileHeight + TileHeight * 0.5f + shakeY;
                    float radius = TileWidth * 2.5f;
                    var inner = new Color4(255, 200, 100, 40);
                    var outer = new Color4(255, 150, 50, 0);
                    r.DrawFilledCircleScreen(cx, cy, radius, inner, outer, radius * 0.3f, 16);
                }
                // Subtle glow behind lava tiles
                else if (tile.Type == TileType.Lava)
                {
                    float cx = sx * TileWidth + TileWidth * 0.5f + shakeX;
                    float cy = sy * TileHeight + TileHeight * 0.5f + shakeY;
                    float radius = TileWidth * 1.5f;
                    var inner = new Color4(255, 80, 20, 25);
                    var outer = new Color4(255, 40, 0, 0);
                    r.DrawFilledCircleScreen(cx, cy, radius, inner, outer, radius * 0.3f, 12);
                }
            }

        // Pass 3: entities
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

    private void RenderHudPanel(ISpriteRenderer r, ClientGameState state, int hudStartCol, int totalRows,
        HudLayout? layout)
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

        var hud = state.PlayerState;
        if (hud == null)
        {
            DrawString(r, col, 1, "No data", ColorDim);
            return;
        }

        if (layout == null)
        {
            // Fallback: render without layout
            RenderHudFallback(r, col, innerW, totalRows, hud, state);
            return;
        }

        layout.ComputeLayout(totalRows);

        foreach (var section in layout.Sections)
        {
            int row = section.StartRow;
            int maxRow = section.StartRow + section.RowCount;

            switch (section.Name)
            {
                case "HP":
                    if (row >= maxRow) break;
                    DrawString(r, col, row, "HP", ColorHpText);
                    row++;
                    if (row >= maxRow) break;
                    int barW = innerW;
                    float hpRatio = hud.MaxHealth > 0 ? (float)hud.Health / hud.MaxHealth : 0;
                    int filled = (int)(barW * hpRatio);
                    for (int i = 0; i < barW; i++)
                        DrawChar(r, col + i, row, i < filled ? '\u2588' : '\u2591', i < filled ? ColorHpFill : ColorHpBar);
                    row++;
                    if (row >= maxRow) break;
                    string hpText = $"{hud.Health}/{hud.MaxHealth}";
                    DrawString(r, col, row, hpText, ColorHpText);
                    break;

                case "Stats":
                    if (row >= maxRow) break;
                    DrawString(r, col, row, $"ATK: {hud.Attack}", ColorStats); row++;
                    if (row >= maxRow) break;
                    DrawString(r, col, row, $"DEF: {hud.Defense}", ColorStats); row++;
                    if (row >= maxRow) break;
                    DrawString(r, col, row, $"Lv:  {hud.Level}", ColorLevel);
                    break;

                case "Skills":
                    if (row >= maxRow) break;
                    DrawString(r, col, row, "Skills", ColorTitle); row++;
                    if (row >= maxRow) break;
                    DrawHudSeparator(r, col, row, innerW); row++;
                    for (int i = 0; i < Math.Min(hud.SkillIds.Length, 2) && row < maxRow; i++)
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
                    break;

                case "Equipment":
                    if (row >= maxRow) break;
                    DrawString(r, col, row, "Equipment", ColorTitle); row++;
                    if (row >= maxRow) break;
                    DrawHudSeparator(r, col, row, innerW); row++;
                    if (row >= maxRow) break;
                    string wpn = !string.IsNullOrEmpty(hud.EquippedWeaponName) ? hud.EquippedWeaponName : "---";
                    string arm = !string.IsNullOrEmpty(hud.EquippedArmorName) ? hud.EquippedArmorName : "---";
                    DrawString(r, col, row, $"W: {wpn}", ColorItem); row++;
                    if (row >= maxRow) break;
                    DrawString(r, col, row, $"A: {arm}", ColorItem);
                    break;

                case "QuickSlots":
                    RenderQuickSlotsSection(r, col, innerW, row, maxRow, hud, layout);
                    break;

                case "FloorItems":
                    RenderFloorItemsSection(r, col, innerW, row, maxRow, state);
                    break;

                case "Controls":
                    if (row >= maxRow) break;
                    DrawString(r, col, row, "[I] Inventory", ColorDim); row++;
                    if (row >= maxRow) break;
                    DrawString(r, col, row, "[Esc] Menu", ColorDim);
                    break;
            }
        }
    }

    private void RenderQuickSlotsSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        Protocol.Messages.PlayerStateMsg hud, HudLayout layout)
    {
        bool focused = layout.FocusedSection?.Name == "QuickSlots";

        if (row >= maxRow) return;
        DrawString(r, col, row, focused ? "\u25ba Quick Use Slots" : "Quick Use Slots", focused ? ColorSelected : ColorTitle);
        row++;
        if (row >= maxRow) return;
        DrawHudSeparator(r, col, row, innerW);
        row++;

        int[] qsIndices = hud.QuickSlotIndices;
        for (int i = 0; i < 4 && row < maxRow; i++)
        {
            int invIdx = i < qsIndices.Length ? qsIndices[i] : -1;
            if (invIdx >= 0 && invIdx < hud.InventoryNames.Length && !string.IsNullOrEmpty(hud.InventoryNames[invIdx]))
            {
                int stack = invIdx < hud.InventoryStackCounts.Length ? hud.InventoryStackCounts[invIdx] : 1;
                string stackStr = stack > 1 ? $"x{stack}" : "";
                DrawString(r, col, row, $"[{i + 1}]{hud.InventoryNames[invIdx]}{stackStr}", ColorItem);
            }
            else
            {
                DrawString(r, col, row, $"[{i + 1}] ---", ColorDim);
            }
            row++;
        }

        if (row < maxRow)
            DrawString(r, col, row, $"Inv:{hud.InventoryCount}/{hud.InventoryCapacity}", ColorInv);
    }

    private void RenderFloorItemsSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        ClientGameState state)
    {
        var floorNames = state.FloorItems?.Names ?? [];
        if (floorNames.Length == 0) return;

        if (row >= maxRow) return;
        DrawString(r, col, row, "On Ground", ColorTitle); row++;
        if (row >= maxRow) return;
        DrawHudSeparator(r, col, row, innerW); row++;
        int floorToShow = Math.Min(floorNames.Length, 4);
        for (int i = 0; i < floorToShow && row < maxRow; i++)
        {
            DrawString(r, col, row, $"  {floorNames[i]}", ColorFloor);
            row++;
        }
        if (row < maxRow)
            DrawString(r, col, row, "[G] Pick up", ColorDim);
    }

    /// <summary>Fallback rendering when no layout is provided (e.g., tests or minimal setup).</summary>
    private void RenderHudFallback(ISpriteRenderer r, int col, int innerW, int totalRows,
        Protocol.Messages.PlayerStateMsg hud, ClientGameState state)
    {
        int row = 1;

        // HP bar
        DrawString(r, col, row, "HP", ColorHpText); row++;
        int barW = innerW;
        float hpRatio = hud.MaxHealth > 0 ? (float)hud.Health / hud.MaxHealth : 0;
        int filled = (int)(barW * hpRatio);
        for (int i = 0; i < barW; i++)
            DrawChar(r, col + i, row, i < filled ? '\u2588' : '\u2591', i < filled ? ColorHpFill : ColorHpBar);
        row++;
        DrawString(r, col, row, $"{hud.Health}/{hud.MaxHealth}", ColorHpText);
        row += 2;

        DrawString(r, col, row, $"ATK: {hud.Attack}", ColorStats); row++;
        DrawString(r, col, row, $"DEF: {hud.Defense}", ColorStats); row++;
        DrawString(r, col, row, $"Lv:  {hud.Level}", ColorLevel);
        row += 2;

        DrawString(r, col, row, "Skills", ColorTitle); row++;
        DrawHudSeparator(r, col, row, innerW); row++;
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

        DrawString(r, col, row, "Equipment", ColorTitle); row++;
        DrawHudSeparator(r, col, row, innerW); row++;
        string wpn = !string.IsNullOrEmpty(hud.EquippedWeaponName) ? hud.EquippedWeaponName : "---";
        string arm = !string.IsNullOrEmpty(hud.EquippedArmorName) ? hud.EquippedArmorName : "---";
        DrawString(r, col, row, $"W: {wpn}", ColorItem); row++;
        DrawString(r, col, row, $"A: {arm}", ColorItem);
        row += 2;

        // Quick Use Slots using protocol data
        DrawString(r, col, row, "Quick Use Slots", ColorTitle); row++;
        DrawHudSeparator(r, col, row, innerW); row++;
        int[] qsIndices = hud.QuickSlotIndices;
        for (int i = 0; i < 4; i++)
        {
            int invIdx = i < qsIndices.Length ? qsIndices[i] : -1;
            if (invIdx >= 0 && invIdx < hud.InventoryNames.Length && !string.IsNullOrEmpty(hud.InventoryNames[invIdx]))
            {
                int stack = invIdx < hud.InventoryStackCounts.Length ? hud.InventoryStackCounts[invIdx] : 1;
                string stackStr = stack > 1 ? $"x{stack}" : "";
                DrawString(r, col, row, $"[{i + 1}]{hud.InventoryNames[invIdx]}{stackStr}", ColorItem);
            }
            else
                DrawString(r, col, row, $"[{i + 1}] ---", ColorDim);
            row++;
        }
        row++;

        DrawString(r, col, row, $"Inv:{hud.InventoryCount}/{hud.InventoryCapacity}", ColorInv);
        row += 2;

        var floorNames = state.FloorItems?.Names ?? [];
        if (floorNames.Length > 0)
        {
            DrawString(r, col, row, "On Ground", ColorTitle); row++;
            DrawHudSeparator(r, col, row, innerW); row++;
            int floorToShow = Math.Min(floorNames.Length, 4);
            for (int i = 0; i < floorToShow; i++)
            {
                DrawString(r, col, row, $"  {floorNames[i]}", ColorFloor);
                row++;
            }
            row++;
            DrawString(r, col, row, "[G] Pick up", ColorDim);
            row++;
        }

        int bottom = totalRows - 2;
        DrawString(r, col, bottom - 1, "[I] Inventory", ColorDim);
        DrawString(r, col, bottom, "[Esc] Menu", ColorDim);
    }

    private void RenderInventoryPanel(ISpriteRenderer r, ClientGameState state, int hudStartCol, int totalRows,
        HudLayout? layout)
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

        var hud = state.PlayerState;
        if (hud == null)
        {
            DrawString(r, col, 1, "No data", ColorDim);
            return;
        }

        if (layout == null)
        {
            RenderInventoryFallback(r, col, innerW, totalRows, hud, 0, 0);
            return;
        }

        layout.ComputeLayout(totalRows);

        foreach (var section in layout.Sections)
        {
            int row = section.StartRow;
            int maxRow = section.StartRow + section.RowCount;
            bool focused = layout.FocusedSection == section;

            switch (section.Name)
            {
                case "InvHeader":
                    if (row >= maxRow) break;
                    DrawString(r, col, row, "INVENTORY", ColorTitle); row++;
                    if (row >= maxRow) break;
                    DrawHudSeparator(r, col, row, innerW); row++;
                    if (row >= maxRow) break;
                    string hpStr = $"HP:{hud.Health}/{hud.MaxHealth}";
                    string atkStr = $"ATK:{hud.Attack}";
                    string defStr = $"DEF:{hud.Defense}";
                    DrawString(r, col, row, hpStr, ColorHpText);
                    DrawString(r, col + hpStr.Length + 1, row, atkStr, ColorStats);
                    DrawString(r, col + hpStr.Length + 1 + atkStr.Length + 1, row, defStr, ColorStats);
                    row++;
                    if (row >= maxRow) break;
                    DrawHudSeparator(r, col, row, innerW);
                    break;

                case "InvItems":
                    RenderInventoryItemsSection(r, col, innerW, row, maxRow, hud, section, focused);
                    break;

                case "InvEquipment":
                    RenderInventoryEquipmentSection(r, col, innerW, row, maxRow, hud, section, focused);
                    break;

                case "InvActions":
                    if (row >= maxRow) break;
                    DrawString(r, col, row, $"Inv:{hud.InventoryCount}/{hud.InventoryCapacity}", ColorInv); row++;
                    row++;
                    if (row >= maxRow) break;
                    DrawHudSeparator(r, col, row, innerW); row++;
                    if (row >= maxRow) break;
                    DrawString(r, col, row, "[Enter] Use / Unequip", ColorDim); row++;
                    if (row >= maxRow) break;
                    DrawString(r, col, row, "[E] Equip  [X] Drop", ColorDim); row++;
                    if (row >= maxRow) break;
                    DrawString(r, col, row, "[1-4] Assign slot", ColorDim); row++;
                    if (row >= maxRow) break;
                    DrawString(r, col, row, "[Tab] Switch section", ColorDim); row++;
                    if (row >= maxRow) break;
                    DrawString(r, col, row, "[Esc] Close", ColorDim);
                    break;
            }
        }
    }

    private void RenderInventoryItemsSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        Protocol.Messages.PlayerStateMsg hud, HudSection section, bool focused)
    {
        int cap = Math.Max(hud.InventoryCapacity, 4);
        int visibleRows = section.RowCount;
        int scrollOffset = section.ScrollOffset;
        int selectedIndex = section.SelectedIndex;
        int[] qsIndices = hud.QuickSlotIndices;

        if (scrollOffset > 0 && row < maxRow)
        {
            DrawString(r, col, row, "  \u2191 more items above", ColorDim);
            row++; visibleRows--;
        }

        bool needsBottomIndicator = scrollOffset + visibleRows < cap;
        int renderRows = needsBottomIndicator ? visibleRows - 1 : visibleRows;

        int visibleEnd = Math.Min(scrollOffset + renderRows, cap);
        for (int i = scrollOffset; i < visibleEnd && row < maxRow; i++)
        {
            bool sel = focused && i == selectedIndex;
            string prefix = sel ? "\u25ba" : " ";

            // Determine quick-slot tag
            string slotTag = "   ";
            for (int q = 0; q < Math.Min(qsIndices.Length, 4); q++)
            {
                if (qsIndices[q] == i) { slotTag = $"[{q + 1}]"; break; }
            }

            string catTag = i < hud.InventoryCategories.Length
                ? CategoryTag(hud.InventoryCategories[i]) : "     ";
            string name = i < hud.InventoryNames.Length && !string.IsNullOrEmpty(hud.InventoryNames[i])
                ? hud.InventoryNames[i] : "---";
            int stack = i < hud.InventoryStackCounts.Length ? hud.InventoryStackCounts[i] : 1;
            string stackStr = stack > 1 ? $" x{stack}" : "";
            string text = $"{prefix}{slotTag}{catTag}{name}{stackStr}";
            if (text.Length > innerW) text = text[..innerW];
            bool isQuickSlot = slotTag != "   ";
            var color = sel ? ColorInvSel : isQuickSlot ? ColorItem : ColorInv;
            DrawString(r, col, row, text, color);
            row++;
        }

        if (needsBottomIndicator && row < maxRow)
        {
            DrawString(r, col, row, "  \u2193 more items below", ColorDim);
        }
    }

    private void RenderInventoryEquipmentSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        Protocol.Messages.PlayerStateMsg hud, HudSection section, bool focused)
    {
        if (row >= maxRow) return;
        DrawString(r, col, row, "Equipment", ColorTitle); row++;
        if (row >= maxRow) return;
        DrawHudSeparator(r, col, row, innerW); row++;

        // Weapon slot (selectedIndex 0 within this section)
        if (row < maxRow)
        {
            bool sel = focused && section.SelectedIndex == 0;
            string prefix = sel ? "\u25ba" : " ";
            string wpn = !string.IsNullOrEmpty(hud.EquippedWeaponName) ? hud.EquippedWeaponName : "---";
            string text = $"{prefix}[W][Wpn]{wpn}";
            if (text.Length > innerW) text = text[..innerW];
            DrawString(r, col, row, text, sel ? ColorInvSel : ColorItem);
            row++;
        }
        // Armor slot (selectedIndex 1 within this section)
        if (row < maxRow)
        {
            bool sel = focused && section.SelectedIndex == 1;
            string prefix = sel ? "\u25ba" : " ";
            string arm = !string.IsNullOrEmpty(hud.EquippedArmorName) ? hud.EquippedArmorName : "---";
            string text = $"{prefix}[A][Arm]{arm}";
            if (text.Length > innerW) text = text[..innerW];
            DrawString(r, col, row, text, sel ? ColorInvSel : ColorItem);
        }
    }

    /// <summary>Fallback inventory rendering when no layout is provided.</summary>
    private void RenderInventoryFallback(ISpriteRenderer r, int col, int innerW, int totalRows,
        Protocol.Messages.PlayerStateMsg hud, int selectedIndex, int scrollOffset)
    {
        int row = 1;
        DrawString(r, col, row, "INVENTORY", ColorTitle); row++;
        DrawHudSeparator(r, col, row, innerW); row++;
        string hpStr = $"HP:{hud.Health}/{hud.MaxHealth}";
        DrawString(r, col, row, hpStr, ColorHpText); row++;
        DrawHudSeparator(r, col, row, innerW); row++;

        int cap = Math.Max(hud.InventoryCapacity, 4);
        for (int i = 0; i < Math.Min(cap, 8) && row < totalRows - 8; i++)
        {
            bool sel = i == selectedIndex;
            string prefix = sel ? "\u25ba" : " ";
            string name = i < hud.InventoryNames.Length && !string.IsNullOrEmpty(hud.InventoryNames[i])
                ? hud.InventoryNames[i] : "---";
            DrawString(r, col, row, $"{prefix}   {name}", sel ? ColorInvSel : ColorInv);
            row++;
        }
    }

    private static string CategoryTag(int category) => category switch
    {
        ItemDefinitions.CategoryWeapon => "[Wpn]",
        ItemDefinitions.CategoryArmor => "[Arm]",
        ItemDefinitions.CategoryPotion => "[Pot]",
        ItemDefinitions.CategoryGold => "[Gld]",
        _ => "     ",
    };

    // ── Connecting Screen ──────────────────────────────────────

    public void RenderConnecting(ISpriteRenderer r, int totalCols, int totalRows, string? errorMessage = null)
    {
        r.DrawRectScreen(0, 0, totalCols * TileWidth, totalRows * TileHeight, ColorBlack);

        int boxW = 50;
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

    public void RenderMainMenu(ISpriteRenderer r, int totalCols, int totalRows, int selectedIndex,
        long worldSeed, bool seedEditing, string seedEditText)
    {
        r.DrawRectScreen(0, 0, totalCols * TileWidth, totalRows * TileHeight, ColorBlack);

        int boxW = 40;
        int boxH = 22;
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

            string label;
            if (i == 2) // Seed item
            {
                string seedDisplay = seedEditing ? seedEditText + "_" : worldSeed.ToString();
                label = prefix + "Seed: " + seedDisplay;
            }
            else
            {
                label = prefix + MainMenuItems[i];
            }

            int tx = bx + 6;
            DrawString(r, tx, itemStartY + i, label, sel ? ColorSelected : ColorNormal);
        }

        // Footer
        string footer = seedEditing
            ? "Type seed   Enter Confirm   Esc Cancel"
            : "\u2191\u2193 Navigate   Enter Select";
        DrawCentered(r, totalCols, by + boxH - 2, footer, ColorDim);
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

    public void RenderPerformanceOverlay(ISpriteRenderer r, int fps, int latencyMs,
        double bwInKBps, double bwOutKBps)
    {
        string fpsText = $"FPS:{fps}";
        string latText = $"Tick:{latencyMs}ms";
        string inText = $"In:{bwInKBps:F1}KB/s";
        string outText = $"Out:{bwOutKBps:F1}KB/s";
        int width = Math.Max(Math.Max(fpsText.Length, latText.Length),
                             Math.Max(inText.Length, outText.Length)) + 1;

        r.DrawRectScreen(0, 0, width * TileWidth, 4 * TileHeight, ColorOverlayBg);

        DrawString(r, 0, 0, fpsText, ColorFps);
        DrawString(r, 0, 1, latText, ColorLatency);
        DrawString(r, 0, 2, inText, ColorLatency);
        DrawString(r, 0, 3, outText, ColorLatency);
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

        float raw = Math.Clamp(lightLevel / 10f, 0f, 1f);
        float brightness = 0.12f + 0.88f * MathF.Pow(raw, 0.65f);
        byte cr = (byte)Math.Min(255, (int)((packedRgb >> 16 & 0xFF) * brightness));
        byte cg = (byte)Math.Min(255, (int)((packedRgb >> 8 & 0xFF) * brightness));
        byte cb = (byte)Math.Min(255, (int)((packedRgb & 0xFF) * brightness));
        return new Color4(cr, cg, cb, 255);
    }

    private static Color4 FogColor(int packedRgb)
    {
        const float dim = 0.22f;
        byte cr = (byte)((packedRgb >> 16 & 0xFF) * dim);
        byte cg = (byte)((packedRgb >> 8 & 0xFF) * dim);
        byte cb = (byte)((packedRgb & 0xFF) * dim);
        return new Color4(cr, cg, cb, 255);
    }


    /// <summary>
    /// Render a minimap in the bottom-right of the HUD panel showing nearby explored tiles.
    /// </summary>
    public void RenderMinimap(ISpriteRenderer r, ClientGameState state, int gameCols, int totalRows)
    {
        int mapSize = 40; // tiles to show in each direction from player
        int pixelSize = 3; // pixels per tile on the minimap
        int minimapPx = mapSize * pixelSize;

        // Position: top-right corner of the gameplay area (left of the HUD)
        float baseX = gameCols * TileWidth - minimapPx - 4;
        float baseY = 4;

        // Background
        r.DrawRectScreen(baseX - 1, baseY - 1, minimapPx + 2, minimapPx + 2, new Color4(40, 40, 50, 200));

        int cx = state.PlayerX;
        int cy = state.PlayerY;
        int half = mapSize / 2;

        for (int dx = 0; dx < mapSize; dx++)
            for (int dy = 0; dy < mapSize; dy++)
            {
                int wx = cx - half + dx;
                int wy = cy - half + dy;

                if (!state.IsExplored(wx, wy))
                    continue;

                var tile = state.GetTile(wx, wy);
                if (tile.GlyphId == 0)
                    continue;

                // Bright for tiles currently visible, dim for explored fog of war
                bool visible = state.IsVisible(wx, wy);

                Color4 dotColor;
                if (tile.Type == TileType.Wall)
                    dotColor = visible ? new Color4(120, 120, 140, 255) : new Color4(50, 50, 60, 255);
                else if (tile.Type == TileType.Lava)
                    dotColor = visible ? new Color4(255, 80, 20, 255) : new Color4(80, 30, 10, 255);
                else if (tile.Type == TileType.Water)
                    dotColor = visible ? new Color4(70, 130, 255, 255) : new Color4(25, 45, 80, 255);
                else if (tile.GlyphId == TileDefinitions.GlyphTorch)
                    dotColor = visible ? new Color4(255, 200, 100, 255) : new Color4(80, 65, 35, 255);
                else if (tile.GlyphId == TileDefinitions.GlyphDoor)
                    dotColor = visible ? new Color4(180, 130, 60, 255) : new Color4(60, 45, 25, 255);
                else if (tile.GlyphId == TileDefinitions.GlyphStairsDown || tile.GlyphId == TileDefinitions.GlyphStairsUp)
                    dotColor = visible ? new Color4(255, 255, 80, 255) : new Color4(80, 80, 30, 255);
                else if (tile.Type == TileType.Decoration)
                    dotColor = visible ? new Color4(80, 80, 60, 255) : new Color4(30, 30, 25, 255);
                else if (tile.Type == TileType.Floor)
                    dotColor = visible ? new Color4(60, 60, 70, 255) : new Color4(25, 25, 30, 255);
                else
                    dotColor = visible ? new Color4(50, 50, 60, 255) : new Color4(20, 20, 25, 255);

                r.DrawRectScreen(baseX + dx * pixelSize, baseY + dy * pixelSize, pixelSize, pixelSize, dotColor);
            }

        // Entities on minimap
        foreach (var entity in state.Entities.Values)
        {
            int dx = entity.X - (cx - half);
            int dy = entity.Y - (cy - half);
            if (dx < 0 || dx >= mapSize || dy < 0 || dy >= mapSize) continue;

            Color4 entityColor = entity.GlyphId == TileDefinitions.GlyphPlayer
                ? new Color4(100, 255, 100, 255)
                : new Color4(255, 80, 80, 255);
            r.DrawRectScreen(baseX + dx * pixelSize, baseY + dy * pixelSize, pixelSize, pixelSize, entityColor);
        }

        // Player dot (always center, drawn last)
        r.DrawRectScreen(baseX + half * pixelSize, baseY + half * pixelSize,
            pixelSize, pixelSize, new Color4(255, 255, 255, 255));
    }



}
