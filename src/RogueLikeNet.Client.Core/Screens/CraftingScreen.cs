using System.Diagnostics;
using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Crafting screen — category selection first, then recipe list, with recent recipes, key repeat, and smart sorting.
/// </summary>
public sealed class CraftingScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly GameWorldRenderer _worldRenderer;
    private readonly OverlayRenderer _overlayRenderer;

    private readonly HudLayout _layout;
    private readonly HudSection _listSection;

    // Navigation state
    private bool _inCategoryMode = true;
    private int[] _categoryOrder = []; // sorted category ids
    private RecipeDefinition[] _filteredRecipes = [];
    private int _selectedCategory;
    private int _savedCategoryIndex;
    private int _savedCategoryScroll;

    // Recent crafted recipes (last 3)
    private readonly List<int> _recentRecipeIds = [];
    private const int MaxRecent = 3;

    // Key repeat
    private static readonly long RepeatDelayTicks = Stopwatch.Frequency / 4; // 250ms
    private InputAction? _heldAction;
    private long _heldSinceTicks;

    public ScreenState ScreenState => ScreenState.Crafting;

    private bool IsDebugFreeCraft => _ctx.Debug is { Enabled: true, FreeCrafting: true };

    public CraftingScreen(ScreenContext ctx, GameWorldRenderer worldRenderer, OverlayRenderer overlayRenderer)
    {
        _ctx = ctx;
        _worldRenderer = worldRenderer;
        _overlayRenderer = overlayRenderer;

        _layout = new HudLayout();
        _layout.AddSection(new HudSection { Name = "CraftHeader", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 3 });
        _listSection = new HudSection { Name = "CraftList", Anchor = HudAnchor.Top, IsFixedHeight = false, Scrollable = true, AcceptsInput = true, UseScrollIndicators = true };
        _layout.AddSection(_listSection);
        _layout.AddSection(new HudSection { Name = "CraftDetail", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 14 });
        _layout.AddSection(new HudSection { Name = "CraftActions", Anchor = HudAnchor.Bottom, IsFixedHeight = true, FixedHeight = 4 });
        _layout.SetFocus(1);
    }

    /// <summary>The item type id of the last successfully crafted recipe, for selecting in inventory.</summary>
    private int _lastCraftedItemTypeId;

    /// <summary>When true, OnEnter preserves the current selection/scroll state instead of resetting.</summary>
    private bool _preserveStateOnEnter;

    public int LastCraftedItemTypeId => _lastCraftedItemTypeId;

    public void OnEnter()
    {
        if (_preserveStateOnEnter)
        {
            _preserveStateOnEnter = false;
            return;
        }
        _inCategoryMode = true;
        _listSection.SelectedIndex = 0;
        _listSection.ScrollOffset = 0;
        RebuildCategoryOrder();
    }

    public void HandleInput(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            if (!_inCategoryMode)
            {
                _inCategoryMode = true;
                RebuildCategoryOrder();
                _listSection.SelectedIndex = _savedCategoryIndex;
                _listSection.ScrollOffset = _savedCategoryScroll;
                return;
            }
            _ctx.RequestTransition(ScreenState.Playing);
            return;
        }

        if (input.IsActionPressed(InputAction.OpenCrafting))
        {
            _ctx.RequestTransition(ScreenState.Playing);
            return;
        }

        if (input.IsActionPressed(InputAction.OpenInventory))
        {
            _preserveStateOnEnter = true; // remember positions when switching back
            _ctx.RequestTransition(ScreenState.Inventory);
            return;
        }

        // Key repeat for up/down
        bool up = false, down = false;
        InputAction? activeNav = null;
        if (input.IsActionDown(InputAction.MenuUp)) activeNav = InputAction.MenuUp;
        else if (input.IsActionDown(InputAction.MenuDown)) activeNav = InputAction.MenuDown;

        if (activeNav != null)
        {
            long now = Stopwatch.GetTimestamp();
            if (_heldAction != activeNav)
            {
                _heldAction = activeNav;
                _heldSinceTicks = now;
            }
            if (input.IsActionPressed(activeNav.Value))
            {
                if (activeNav == InputAction.MenuUp) up = true; else down = true;
            }
            else if (now - _heldSinceTicks >= RepeatDelayTicks)
            {
                _heldSinceTicks = now;
                if (activeNav == InputAction.MenuUp) up = true; else down = true;
            }
        }
        else
        {
            _heldAction = null;
        }

        if (_inCategoryMode)
        {
            int count = _categoryOrder.Length;
            if (up) _listSection.ScrollUp();
            else if (down) _listSection.ScrollDown(count);
            else if (input.IsActionPressed(InputAction.MenuConfirm))
            {
                int idx = _listSection.SelectedIndex;
                if (idx >= 0 && idx < _categoryOrder.Length)
                {
                    _selectedCategory = _categoryOrder[idx];
                    _savedCategoryIndex = idx;
                    _savedCategoryScroll = _listSection.ScrollOffset;
                    _inCategoryMode = false;
                    RebuildFilteredRecipes();
                    _listSection.SelectedIndex = 0;
                    _listSection.ScrollOffset = 0;
                }
            }
        }
        else
        {
            int count = _filteredRecipes.Length;
            if (up) _listSection.ScrollUp();
            else if (down) _listSection.ScrollDown(count);
            else if (input.IsActionPressed(InputAction.MenuConfirm))
                TryCraft();
        }
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

        RenderCraftingPanel(renderer, gameCols, totalRows);

        int halfW = zoomedGameCols / 2;
        int halfH = zoomedRows / 2;
        _ctx.Particles.Render(renderer, _ctx.GameState.PlayerX, _ctx.GameState.PlayerY,
            halfW, halfH, shakeX, shakeY, tileW, tileH);

        _overlayRenderer.RenderChat(renderer, totalCols, totalRows, _ctx.Chat);
        _overlayRenderer.RenderPerformance(renderer, _ctx.Performance, _ctx.Debug);
    }

    private void RenderCraftingPanel(ISpriteRenderer r, int hudStartCol, int totalRows)
    {
        float hx = hudStartCol * AsciiDraw.TileWidth;
        r.DrawRectScreen(hx, 0, AsciiDraw.HudColumns * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.HudBg);

        AsciiDraw.DrawChar(r, hudStartCol, 0, '\u252C', RenderingTheme.Border);
        for (int y = 1; y < totalRows - 1; y++)
            AsciiDraw.DrawChar(r, hudStartCol, y, '\u2502', RenderingTheme.Border);
        AsciiDraw.DrawChar(r, hudStartCol, totalRows - 1, '\u2534', RenderingTheme.Border);

        int col = hudStartCol + 1;
        int innerW = AsciiDraw.HudColumns - 1;

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
                    string title = _inCategoryMode ? "CRAFTING" : $"CRAFTING > {ItemDefinition.CategoryName((ItemCategory)_selectedCategory)}";
                    if (title.Length > innerW) title = title[..innerW];
                    AsciiDraw.DrawString(r, col, row, title, RenderingTheme.Title);
                    AsciiDraw.DrawString(r, col + innerW - 5, row, "[Esc]", RenderingTheme.Dim);
                    row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW);
                    break;

                case "CraftList":
                    if (_inCategoryMode)
                        RenderCategoryList(r, col, innerW, row, maxRow, hud, section);
                    else
                        RenderRecipesSection(r, col, innerW, row, maxRow, hud, section);
                    break;

                case "CraftDetail":
                    if (!_inCategoryMode)
                        RenderDetailSection(r, col, innerW, row, maxRow, hud);
                    else
                        RenderRecentRecipes(r, col, innerW, row, maxRow, hud);
                    break;

                case "CraftActions":
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;
                    if (_inCategoryMode)
                    {
                        if (row < maxRow) { AsciiDraw.DrawString(r, col, row, "[Enter] Open category", RenderingTheme.Dim); row++; }
                        if (row < maxRow) { AsciiDraw.DrawString(r, col, row, "[\u2191\u2193] Select category", RenderingTheme.Dim); row++; }
                    }
                    else
                    {
                        if (row < maxRow) { AsciiDraw.DrawString(r, col, row, "[Enter] Craft", RenderingTheme.Dim); row++; }
                        if (row < maxRow) { AsciiDraw.DrawString(r, col, row, "[\u2191\u2193] Select recipe", RenderingTheme.Dim); row++; }
                    }
                    break;
            }
        }
    }

    private void RenderCategoryList(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg hud, HudSection section)
    {
        int count = _categoryOrder.Length;
        int scrollOffset = section.ScrollOffset;
        int selectedIndex = section.SelectedIndex;
        int visibleRows = maxRow - row;

        bool showTopArrow = scrollOffset > 0;
        bool showBottomArrow = scrollOffset + visibleRows < count;

        int renderEnd = Math.Min(scrollOffset + visibleRows, count);
        for (int i = scrollOffset; i < renderEnd && row < maxRow; i++)
        {
            int cat = _categoryOrder[i];
            bool hasCraftable = CategoryHasCraftable(cat, hud);
            bool sel = i == selectedIndex;

            string prefix = sel ? "\u25ba" : " ";
            int totalCount = CountRecipesInCategory(cat);
            int craftableCount = CountCraftableInCategory(cat, hud);
            string text = $"{prefix}{ItemDefinition.CategoryName((ItemCategory)cat)} ({craftableCount}/{totalCount})";
            if (hasCraftable) text += " \u2605";
            if (text.Length > innerW) text = text[..innerW];

            var color = sel ? RenderingTheme.InvSel : hasCraftable ? RenderingTheme.Item : RenderingTheme.Dim;
            AsciiDraw.DrawString(r, col, row, text, color);

            if (i == scrollOffset && showTopArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2191', RenderingTheme.Dim);
            if (i == renderEnd - 1 && showBottomArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2193', RenderingTheme.Dim);
            row++;
        }
    }

    private void RenderRecipesSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg hud, HudSection section)
    {
        int recipeCount = _filteredRecipes.Length;
        int scrollOffset = section.ScrollOffset;
        int selectedIndex = section.SelectedIndex;
        int visibleRows = maxRow - row;

        bool showTopArrow = scrollOffset > 0;
        bool showBottomArrow = scrollOffset + visibleRows < recipeCount;

        int renderEnd = Math.Min(scrollOffset + visibleRows, recipeCount);
        for (int i = scrollOffset; i < renderEnd && row < maxRow; i++)
        {
            var recipe = _filteredRecipes[i];
            bool canCraft = CanCraftRecipe(recipe, hud, IsDebugFreeCraft);
            bool sel = i == selectedIndex;

            string prefix = sel ? "\u25ba" : " ";
            var def = GameData.Instance.Items.Get(recipe.Result.NumericItemId);
            string tag = def != null ? AsciiDraw.CategoryTag(def.CategoryInt) : "     ";
            string text = $"{prefix}{tag}{recipe.Name}";
            if (text.Length > innerW) text = text[..innerW];

            var color = sel ? RenderingTheme.InvSel : canCraft ? RenderingTheme.Item : RenderingTheme.Dim;
            AsciiDraw.DrawString(r, col, row, text, color);

            if (i == scrollOffset && showTopArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2191', RenderingTheme.Dim);
            if (i == renderEnd - 1 && showBottomArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2193', RenderingTheme.Dim);
            row++;
        }
    }

    private void RenderRecentRecipes(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg hud)
    {
        if (_recentRecipeIds.Count == 0 || row >= maxRow) return;

        AsciiDraw.DrawString(r, col, row, "Recent:", RenderingTheme.Dim); row++;
        if (row >= maxRow) return;
        AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;

        var recipes = GameData.Instance.Recipes;
        foreach (var recipeId in _recentRecipeIds)
        {
            if (row >= maxRow) break;
            var recipe = recipes.Get(recipeId);
            if (recipe == null) continue;
            bool canCraft = CanCraftRecipe(recipe, hud, IsDebugFreeCraft);
            var def = GameData.Instance.Items.Get(recipe.Result.NumericItemId);
            string tag = def != null ? AsciiDraw.CategoryTag(def.CategoryInt) : "     ";
            string text = $"  {tag}{recipe.Name}";
            if (text.Length > innerW) text = text[..innerW];
            AsciiDraw.DrawString(r, col, row, text, canCraft ? RenderingTheme.Item : RenderingTheme.Dim);
            row++;
        }
    }

    private void RenderDetailSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg hud)
    {
        if (row >= maxRow) return;
        AsciiDraw.DrawString(r, col, row, "Ingredients:", RenderingTheme.Dim); row++;
        if (row >= maxRow) return;
        AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;

        int selIdx = _listSection.SelectedIndex;
        if (selIdx < 0 || selIdx >= _filteredRecipes.Length) return;

        var recipe = _filteredRecipes[selIdx];
        foreach (var ingredient in recipe.Ingredients)
        {
            if (row >= maxRow) break;
            var def = GameData.Instance.Items.Get(ingredient.NumericItemId);
            int have = CountItem(hud, ingredient.NumericItemId);
            bool enough = have >= ingredient.Count;
            var color = enough ? RenderingTheme.StatPositive : RenderingTheme.StatNegative;
            string text = $"  {def?.Name ?? "Unknown"}: {have}/{ingredient.Count}";
            if (text.Length > innerW) text = text[..innerW];
            AsciiDraw.DrawString(r, col, row, text, color);
            row++;
        }

        if (row < maxRow) { AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++; }
        if (row < maxRow)
        {
            int owned = CountItem(hud, recipe.Result.NumericItemId);
            string ownedText = $"  Owned: {owned}";
            AsciiDraw.DrawString(r, col, row, ownedText, owned > 0 ? RenderingTheme.Stats : RenderingTheme.Dim);
            row++;
        }

        if (row < maxRow)
        {
            bool canCraft = CanCraftRecipe(recipe, hud, IsDebugFreeCraft);
            bool hasStation = HasNearbyStation(recipe, hud);
            if (!hasStation)
            {
                string stationText = $">> Need: {StationName(recipe.Station)}";
                AsciiDraw.DrawString(r, col, row, stationText, RenderingTheme.StatNegative);
            }
            else
            {
                var statusColor = canCraft ? RenderingTheme.StatPositive : RenderingTheme.StatNegative;
                string status = canCraft ? ">> Ready to craft" : ">> Missing resources";
                AsciiDraw.DrawString(r, col, row, status, statusColor);
            }
            row++;
        }

        if (row < maxRow && recipe.Station != CraftingStationType.Hand)
        {
            bool hasStation = HasNearbyStation(recipe, hud);
            var stationColor = hasStation ? RenderingTheme.Stats : RenderingTheme.Dim;
            string stationLabel = $"  Station: {StationName(recipe.Station)}";
            if (hasStation) stationLabel += " \u2713";
            if (stationLabel.Length > innerW) stationLabel = stationLabel[..innerW];
            AsciiDraw.DrawString(r, col, row, stationLabel, stationColor);
        }
    }

    private void TryCraft()
    {
        if (_ctx.Connection == null) return;
        int selIdx = _listSection.SelectedIndex;
        if (selIdx < 0 || selIdx >= _filteredRecipes.Length) return;

        var hud = _ctx.GameState.PlayerState;
        if (hud == null) return;

        var recipe = _filteredRecipes[selIdx];
        bool debugFree = _ctx.Debug is { Enabled: true, FreeCrafting: true };
        if (!debugFree && !CanCraftRecipe(recipe, hud)) return;

        // Track recent recipe
        _recentRecipeIds.Remove(recipe.NumericId);
        _recentRecipeIds.Insert(0, recipe.NumericId);
        if (_recentRecipeIds.Count > MaxRecent)
            _recentRecipeIds.RemoveAt(_recentRecipeIds.Count - 1);

        _lastCraftedItemTypeId = recipe.Result.NumericItemId;

        var msg = new ClientInputMsg
        {
            ActionType = ActionTypes.Craft,
            ItemSlot = recipe.NumericId,
            Tick = _ctx.GameState.WorldTick
        };
        _ = _ctx.Connection.SendInputAsync(msg);
    }

    private void RebuildCategoryOrder()
    {
        var playerState = _ctx.GameState.PlayerState;
        var recipes = GameData.Instance.Recipes.All;

        // Collect unique categories
        var categories = new HashSet<int>();
        foreach (var r in recipes)
        {
            var def = GameData.Instance.Items.Get(r.Result.NumericItemId);
            if (def != null) categories.Add(def.CategoryInt);
        }

        // Sort: categories with craftable recipes first, then by category id
        var sorted = categories.ToList();
        sorted.Sort((a, b) =>
        {
            bool aCraft = playerState != null && CategoryHasCraftable(a, playerState);
            bool bCraft = playerState != null && CategoryHasCraftable(b, playerState);
            if (aCraft != bCraft) return aCraft ? -1 : 1;
            return a.CompareTo(b);
        });
        _categoryOrder = sorted.ToArray();
    }

    private void RebuildFilteredRecipes()
    {
        var hud = _ctx.GameState.PlayerState;
        var recipes = GameData.Instance.Recipes.All;
        var filtered = new List<RecipeDefinition>();

        foreach (var r in recipes)
        {
            var def = GameData.Instance.Items.Get(r.Result.NumericItemId);
            if (def != null && def.CategoryInt == _selectedCategory)
                filtered.Add(r);
        }

        // Sort: craftable first, then by name
        filtered.Sort((a, b) =>
        {
            bool aCraft = hud != null && CanCraftRecipe(a, hud, IsDebugFreeCraft);
            bool bCraft = hud != null && CanCraftRecipe(b, hud, IsDebugFreeCraft);
            if (aCraft != bCraft) return aCraft ? -1 : 1;
            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });
        _filteredRecipes = filtered.ToArray();
    }

    private bool CategoryHasCraftable(int category, PlayerStateMsg playerState)
    {
        foreach (var r in GameData.Instance.Recipes.All)
        {
            var def = GameData.Instance.Items.Get(r.Result.NumericItemId);
            if (def != null && def.CategoryInt == category && CanCraftRecipe(r, playerState, IsDebugFreeCraft))
                return true;
        }
        return false;
    }

    private static int CountRecipesInCategory(int category)
    {
        int count = 0;
        foreach (var r in GameData.Instance.Recipes.All)
        {
            var def = GameData.Instance.Items.Get(r.Result.NumericItemId);
            if (def != null && def.CategoryInt == category) count++;
        }
        return count;
    }

    private int CountCraftableInCategory(int category, PlayerStateMsg playerState)
    {
        int count = 0;
        foreach (var r in GameData.Instance.Recipes.All)
        {
            var def = GameData.Instance.Items.Get(r.Result.NumericItemId);
            if (def != null && def.CategoryInt == category && CanCraftRecipe(r, playerState, IsDebugFreeCraft)) count++;
        }
        return count;
    }

    private static bool CanCraftRecipe(RecipeDefinition recipe, PlayerStateMsg playerState, bool debugFreeCraft = false)
    {
        if (debugFreeCraft) return true;

        // Check station availability
        if (!HasNearbyStation(recipe, playerState))
            return false;

        foreach (var ingredient in recipe.Ingredients)
        {
            if (CountItem(playerState, ingredient.NumericItemId) < ingredient.Count)
                return false;
        }
        return true;
    }

    private static bool HasNearbyStation(RecipeDefinition recipe, PlayerStateMsg playerState)
    {
        int stationId = (int)recipe.Station;
        foreach (var s in playerState.NearbyStationsTypes)
        {
            if (s == stationId) return true;
        }
        return false;
    }

    private static string StationName(CraftingStationType station) => station switch
    {
        CraftingStationType.Hand => "Hand",
        CraftingStationType.Workbench => "Workbench",
        CraftingStationType.Forge => "Forge",
        CraftingStationType.Anvil => "Anvil",
        CraftingStationType.Furnace => "Furnace",
        CraftingStationType.CookingPot => "Cooking Pot",
        CraftingStationType.Alchemy => "Alchemy Table",
        CraftingStationType.Loom => "Loom",
        CraftingStationType.TanningRack => "Tanning Rack",
        CraftingStationType.StoneCutter => "Stone Cutter",
        CraftingStationType.Sawmill => "Sawmill",
        _ => "Unknown",
    };

    private static int CountItem(PlayerStateMsg playerState, int itemTypeId)
    {
        int count = 0;
        foreach (var item in playerState.InventoryItems)
        {
            if (item.ItemTypeId == itemTypeId)
                count += item.StackCount;
        }
        return count;
    }
}
