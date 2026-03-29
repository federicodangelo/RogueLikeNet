using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Crafting screen — displays available recipes, ingredient requirements, and allows crafting buildable items.
/// </summary>
public sealed class CraftingScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly GameWorldRenderer _worldRenderer;
    private readonly OverlayRenderer _overlayRenderer;

    private readonly HudLayout _layout;
    private readonly HudSection _recipesSection;

    public ScreenState ScreenState => ScreenState.Crafting;

    public CraftingScreen(ScreenContext ctx, GameWorldRenderer worldRenderer, OverlayRenderer overlayRenderer)
    {
        _ctx = ctx;
        _worldRenderer = worldRenderer;
        _overlayRenderer = overlayRenderer;

        _layout = new HudLayout();
        _layout.AddSection(new HudSection { Name = "CraftHeader", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 3 });
        _recipesSection = new HudSection { Name = "CraftRecipes", Anchor = HudAnchor.Top, IsFixedHeight = false, Scrollable = true, AcceptsInput = true, UseScrollIndicators = true };
        _layout.AddSection(_recipesSection);
        _layout.AddSection(new HudSection { Name = "CraftDetail", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 14 });
        _layout.AddSection(new HudSection { Name = "CraftActions", Anchor = HudAnchor.Bottom, IsFixedHeight = true, FixedHeight = 5 });
        _layout.SetFocus(1);
    }

    public void OnEnter()
    {
        _recipesSection.SelectedIndex = 0;
        _recipesSection.ScrollOffset = 0;
    }

    public void HandleInput(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _ctx.RequestTransition(ScreenState.Playing);
            return;
        }

        int recipeCount = CraftingDefinitions.All.Length;

        if (input.IsActionPressed(InputAction.MenuUp))
            _recipesSection.ScrollUp();
        else if (input.IsActionPressed(InputAction.MenuDown))
            _recipesSection.ScrollDown(recipeCount);
        else if (input.IsActionPressed(InputAction.MenuConfirm))
            TryCraft();
    }

    public void Update(float deltaTime)
    {
        _ctx.Particles.Update(deltaTime);
        _ctx.ScreenShake.Update(_ctx.GameState.PlayerState?.Health ?? 0);
    }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        int gameCols = totalCols - AsciiDraw.HudColumns;
        float shakeX = _ctx.ScreenShake.OffsetX;
        float shakeY = _ctx.ScreenShake.OffsetY;

        var debug = _ctx.Debug;
        int tileW = debug.EffectiveTileWidth;
        int tileH = debug.EffectiveTileHeight;
        float fontScale = debug.EffectiveFontScale;

        int gamePixelW = gameCols * AsciiDraw.TileWidth;
        int gamePixelH = totalRows * AsciiDraw.TileHeight;
        int zoomedGameCols = Math.Max(1, gamePixelW / tileW);
        int zoomedRows = Math.Max(1, gamePixelH / tileH);

        renderer.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);
        bool debugLightOff = debug is { Enabled: true, LightOff: true };
        _worldRenderer.Render(renderer, _ctx.GameState, zoomedGameCols, zoomedRows, shakeX, shakeY, tileW, tileH, fontScale, debugLightOff);

        // Render crafting HUD panel
        RenderCraftingPanel(renderer, gameCols, totalRows);

        int halfW = zoomedGameCols / 2;
        int halfH = zoomedRows / 2;
        _ctx.Particles.Render(renderer, _ctx.GameState.PlayerX, _ctx.GameState.PlayerY,
            halfW, halfH, shakeX, shakeY);

        _overlayRenderer.RenderChat(renderer, totalCols, totalRows, _ctx.Chat);
        _overlayRenderer.RenderPerformance(renderer, _ctx.Performance, _ctx.Debug);
    }

    private void RenderCraftingPanel(ISpriteRenderer r, int hudStartCol, int totalRows)
    {
        float hx = hudStartCol * AsciiDraw.TileWidth;
        r.DrawRectScreen(hx, 0, AsciiDraw.HudColumns * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.HudBg);

        // Vertical separator
        AsciiDraw.DrawChar(r, hudStartCol, 0, '\u252C', RenderingTheme.Border);
        for (int y = 1; y < totalRows - 1; y++)
            AsciiDraw.DrawChar(r, hudStartCol, y, '\u2502', RenderingTheme.Border);
        AsciiDraw.DrawChar(r, hudStartCol, totalRows - 1, '\u2534', RenderingTheme.Border);

        int col = hudStartCol + 1;
        int innerW = AsciiDraw.HudColumns - 2;

        var hud = _ctx.GameState.PlayerState;
        if (hud == null)
        {
            AsciiDraw.DrawString(r, col, 1, "No data", RenderingTheme.Dim);
            return;
        }

        _layout.ComputeLayout(totalRows);

        foreach (var section in _layout.Sections)
        {
            int row = section.StartRow;
            int maxRow = section.StartRow + section.RowCount;

            switch (section.Name)
            {
                case "CraftHeader":
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, "CRAFTING", RenderingTheme.Title); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW);
                    break;

                case "CraftRecipes":
                    RenderRecipesSection(r, col, innerW, row, maxRow, hud, section);
                    break;

                case "CraftDetail":
                    RenderDetailSection(r, col, innerW, row, maxRow, hud);
                    break;

                case "CraftActions":
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, "[Enter] Craft", RenderingTheme.Dim); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, "[\u2191\u2193] Select recipe", RenderingTheme.Dim); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, "[Esc] Close", RenderingTheme.Dim);
                    break;
            }
        }
    }

    private void RenderRecipesSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg hud, HudSection section)
    {
        var recipes = CraftingDefinitions.All;
        int recipeCount = recipes.Length;
        int scrollOffset = section.ScrollOffset;
        int selectedIndex = section.SelectedIndex;
        int visibleRows = section.RowCount;

        if (scrollOffset > 0 && row < maxRow)
        {
            AsciiDraw.DrawString(r, col, row, "  \u2191 more above", RenderingTheme.Dim);
            row++; visibleRows--;
        }

        bool needsBottom = scrollOffset + visibleRows < recipeCount;
        int renderRows = needsBottom ? visibleRows - 1 : visibleRows;

        int visibleEnd = Math.Min(scrollOffset + renderRows, recipeCount);
        for (int i = scrollOffset; i < visibleEnd && row < maxRow; i++)
        {
            var recipe = recipes[i];
            bool canCraft = CanCraftRecipe(recipe, hud);
            bool sel = i == selectedIndex;

            string prefix = sel ? "\u25ba" : " ";
            string text = $"{prefix}[Bld]{recipe.Name}";
            if (text.Length > innerW) text = text[..innerW];

            var color = sel ? RenderingTheme.InvSel : canCraft ? RenderingTheme.Item : RenderingTheme.Dim;
            AsciiDraw.DrawString(r, col, row, text, color);
            row++;
        }

        if (needsBottom && row < maxRow)
        {
            AsciiDraw.DrawString(r, col, row, "  \u2193 more below", RenderingTheme.Dim);
        }
    }

    private void RenderDetailSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg hud)
    {
        if (row >= maxRow) return;
        AsciiDraw.DrawString(r, col, row, "Ingredients:", RenderingTheme.Dim); row++;
        if (row >= maxRow) return;
        AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;

        var recipes = CraftingDefinitions.All;
        int selIdx = _recipesSection.SelectedIndex;
        if (selIdx < 0 || selIdx >= recipes.Length) return;

        var recipe = recipes[selIdx];
        foreach (var ingredient in recipe.Ingredients)
        {
            if (row >= maxRow) break;
            var def = ItemDefinitions.Get(ingredient.ItemTypeId);
            int have = CountItem(hud, ingredient.ItemTypeId);
            bool enough = have >= ingredient.Count;
            var color = enough ? RenderingTheme.StatPositive : RenderingTheme.StatNegative;
            string text = $"  {def.Name}: {have}/{ingredient.Count}";
            if (text.Length > innerW) text = text[..innerW];
            AsciiDraw.DrawString(r, col, row, text, color);
            row++;
        }

        // Show how many of the crafted item the player already owns
        if (row < maxRow) { AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++; }
        if (row < maxRow)
        {
            int owned = CountItem(hud, recipe.ResultItemTypeId);
            string ownedText = $"  Owned: {owned}";
            AsciiDraw.DrawString(r, col, row, ownedText, owned > 0 ? RenderingTheme.Stats : RenderingTheme.Dim);
            row++;
        }

        if (row < maxRow)
        {
            bool canCraft = CanCraftRecipe(recipe, hud);
            var statusColor = canCraft ? RenderingTheme.StatPositive : RenderingTheme.StatNegative;
            string status = canCraft ? ">> Ready to craft" : ">> Missing resources";
            AsciiDraw.DrawString(r, col, row, status, statusColor);
        }
    }

    private void TryCraft()
    {
        if (_ctx.Connection == null) return;
        var recipes = CraftingDefinitions.All;
        int selIdx = _recipesSection.SelectedIndex;
        if (selIdx < 0 || selIdx >= recipes.Length) return;

        var hud = _ctx.GameState.PlayerState;
        if (hud == null) return;

        if (!CanCraftRecipe(recipes[selIdx], hud)) return;

        var msg = new ClientInputMsg
        {
            ActionType = ActionTypes.Craft,
            ItemSlot = selIdx,
            Tick = _ctx.GameState.WorldTick
        };
        _ = _ctx.Connection.SendInputAsync(msg);
    }

    private static bool CanCraftRecipe(CraftingRecipe recipe, PlayerStateMsg hud)
    {
        foreach (var ingredient in recipe.Ingredients)
        {
            if (CountItem(hud, ingredient.ItemTypeId) < ingredient.Count)
                return false;
        }
        return true;
    }

    private static int CountItem(PlayerStateMsg hud, int itemTypeId)
    {
        int count = 0;
        foreach (var item in hud.InventoryItems)
        {
            if (item.ItemTypeId == itemTypeId)
                count += item.StackCount;
        }
        return count;
    }
}
