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
    private int[] _internalCategoryIdsInOrder = [];
    private RecipeDefinition[] _filteredRecipes = [];
    private int _selectedInternalCategoryId;
    private int _savedCategoryIndex;
    private int _savedCategoryScroll;

    // Recent crafted recipes (last 3)
    private readonly List<int> _recentRecipeIds = [];
    private const int MaxRecent = 3;

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

        if (_inCategoryMode)
        {
            int count = _internalCategoryIdsInOrder.Length;
            if (input.IsActionPressedOrRepeated(InputAction.MenuUp)) _listSection.ScrollUp();
            else if (input.IsActionPressedOrRepeated(InputAction.MenuDown)) _listSection.ScrollDown(count);
            else if (input.IsActionPressed(InputAction.MenuConfirm))
            {
                int idx = _listSection.SelectedIndex;
                if (idx >= 0 && idx < _internalCategoryIdsInOrder.Length)
                {
                    _selectedInternalCategoryId = _internalCategoryIdsInOrder[idx];
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
            if (input.IsActionPressedOrRepeated(InputAction.MenuUp)) _listSection.ScrollUp();
            else if (input.IsActionPressedOrRepeated(InputAction.MenuDown)) _listSection.ScrollDown(count);
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

        var playerState = _ctx.GameState.PlayerState;
        if (playerState == null)
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
                    string title = _inCategoryMode ? "CRAFTING" : $"CRAFTING > {InternalCategoryName(_selectedInternalCategoryId)}";
                    if (title.Length > innerW) title = title[..innerW];
                    AsciiDraw.DrawString(r, col, row, title, RenderingTheme.Title);
                    AsciiDraw.DrawString(r, col + innerW - 5, row, "[ESC]", RenderingTheme.Dim);
                    row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW);
                    break;

                case "CraftList":
                    if (_inCategoryMode)
                        RenderCategoryList(r, col, innerW, row, maxRow, playerState, section);
                    else
                        RenderRecipesSection(r, col, innerW, row, maxRow, playerState, section);
                    break;

                case "CraftDetail":
                    if (!_inCategoryMode)
                        RenderDetailSection(r, col, innerW, row, maxRow, playerState);
                    else
                        RenderRecentRecipes(r, col, innerW, row, maxRow, playerState);
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
        PlayerStateMsg playerState, HudSection section)
    {
        int count = _internalCategoryIdsInOrder.Length;
        int scrollOffset = section.ScrollOffset;
        int selectedIndex = section.SelectedIndex;
        int visibleRows = maxRow - row;

        bool showTopArrow = scrollOffset > 0;
        bool showBottomArrow = scrollOffset + visibleRows < count;

        int renderEnd = Math.Min(scrollOffset + visibleRows, count);
        for (int i = scrollOffset; i < renderEnd && row < maxRow; i++)
        {
            int cat = _internalCategoryIdsInOrder[i];
            bool hasCraftable = CategoryHasCraftable(cat, playerState);
            bool sel = i == selectedIndex;

            string prefix = sel ? "\u25ba" : " ";
            int totalCount = CountRecipesInCategory(cat);
            int craftableCount = CountCraftableInCategory(cat, playerState);
            string text = $"{prefix}{InternalCategoryName(cat)} ({craftableCount}/{totalCount})";
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
        PlayerStateMsg playerState, HudSection section)
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
            bool canCraft = CanCraftRecipe(recipe, playerState, IsDebugFreeCraft);
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
        PlayerStateMsg playerState)
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
            bool canCraft = CanCraftRecipe(recipe, playerState, IsDebugFreeCraft);
            var def = GameData.Instance.Items.Get(recipe.Result.NumericItemId);
            string tag = def != null ? AsciiDraw.CategoryTag(def.CategoryInt) : "     ";
            string text = $"  {tag}{recipe.Name}";
            if (text.Length > innerW) text = text[..innerW];
            AsciiDraw.DrawString(r, col, row, text, canCraft ? RenderingTheme.Item : RenderingTheme.Dim);
            row++;
        }
    }

    private void RenderDetailSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg playerState)
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
            int have = CountItem(playerState, ingredient.NumericItemId);
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
            int owned = CountItem(playerState, recipe.Result.NumericItemId);
            string ownedText = $"  Owned: {owned}";
            AsciiDraw.DrawString(r, col, row, ownedText, owned > 0 ? RenderingTheme.Stats : RenderingTheme.Dim);
            row++;
        }

        if (row < maxRow)
        {
            bool canCraft = CanCraftRecipe(recipe, playerState, IsDebugFreeCraft);
            bool hasStation = HasNearbyStation(recipe, playerState);
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
            bool hasStation = HasNearbyStation(recipe, playerState);
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
            if (def != null) categories.Add(InternalCategoryId(def));
        }

        // Sort: categories with craftable recipes first, then by category Name
        var sorted = categories.ToList();
        sorted.Sort((a, b) =>
        {
            bool aCraft = playerState != null && CategoryHasCraftable(a, playerState);
            bool bCraft = playerState != null && CategoryHasCraftable(b, playerState);
            if (aCraft != bCraft) return aCraft ? -1 : 1;
            var aName = InternalCategoryName(a);
            var bName = InternalCategoryName(b);
            return aName.CompareTo(bName, StringComparison.InvariantCultureIgnoreCase);
        });
        _internalCategoryIdsInOrder = sorted.ToArray();
    }

    private void RebuildFilteredRecipes()
    {
        var hud = _ctx.GameState.PlayerState;
        var recipes = GameData.Instance.Recipes.All;
        var filtered = new List<RecipeDefinition>();

        foreach (var r in recipes)
        {
            var def = GameData.Instance.Items.Get(r.Result.NumericItemId);
            if (def != null && InternalCategoryId(def) == _selectedInternalCategoryId)
                filtered.Add(r);
        }

        // Sort: craftable first, then by name
        filtered.Sort((a, b) =>
        {
            bool aCraft = hud != null && CanCraftRecipe(a, hud, IsDebugFreeCraft);
            bool bCraft = hud != null && CanCraftRecipe(b, hud, IsDebugFreeCraft);
            if (aCraft != bCraft) return aCraft ? -1 : 1;
            return string.Compare(a.Name, b.Name, StringComparison.InvariantCultureIgnoreCase);
        });
        _filteredRecipes = filtered.ToArray();
    }

    private bool CategoryHasCraftable(int category, PlayerStateMsg playerState)
    {
        foreach (var r in GameData.Instance.Recipes.All)
        {
            var def = GameData.Instance.Items.Get(r.Result.NumericItemId);
            if (def != null && InternalCategoryId(def) == category && CanCraftRecipe(r, playerState, IsDebugFreeCraft))
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
            if (def != null && InternalCategoryId(def) == category) count++;
        }
        return count;
    }

    private int CountCraftableInCategory(int category, PlayerStateMsg playerState)
    {
        int count = 0;
        foreach (var r in GameData.Instance.Recipes.All)
        {
            var def = GameData.Instance.Items.Get(r.Result.NumericItemId);
            if (def != null && InternalCategoryId(def) == category && CanCraftRecipe(r, playerState, IsDebugFreeCraft)) count++;
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


    /// <summary>
    /// Returns an effective category ID for filtering.
    /// Placeable items return a sub-category ID encoding their PlaceableType
    /// (PlaceableCategoryBase + PlaceableType). All other items return CategoryInt.
    /// </summary>
    static private int InternalCategoryId(ItemDefinition itemDefinition) => itemDefinition.Category == ItemCategory.Placeable && itemDefinition.Placeable != null
        ? InternalPlaceableCategoryId(itemDefinition.Placeable.PlaceableType)
        : itemDefinition.CategoryInt;

    /// <summary>Base value for placeable sub-category IDs (PlaceableCategoryBase + PlaceableType).</summary>
    private const int PlaceableCategoryBase = 1000;

    /// <summary>Returns the encoded filter ID for a given placeable type.</summary>
    private static int InternalPlaceableCategoryId(PlaceableType pt) => PlaceableCategoryBase + (int)pt;

    /// <summary>Returns true if <paramref name="categoryId"/> is a placeable sub-category.</summary>
    private static bool IsPlaceableSubCategory(int categoryId) => categoryId >= PlaceableCategoryBase;

    /// <summary>Returns the display name for any category ID, including placeable sub-categories.</summary>
    private static string InternalCategoryName(int categoryId) => IsPlaceableSubCategory(categoryId)
        ? ItemDefinition.PlaceableTypeName((PlaceableType)(categoryId - PlaceableCategoryBase))
        : ItemDefinition.CategoryName((ItemCategory)categoryId);
}
