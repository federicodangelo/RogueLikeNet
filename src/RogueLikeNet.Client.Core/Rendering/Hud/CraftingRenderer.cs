using Engine.Platform;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Rendering.Hud;

/// <summary>
/// Renders the crafting HUD panel: category mode (top-level list of categories with craftable counts),
/// or recipe mode (filtered recipes for a category, with ingredient detail and station status).
/// Owns the panel's HudLayout (and thus the scrollable list section) since layout and scroll state
/// are tightly coupled to what is being rendered.
/// </summary>
public sealed class CraftingRenderer
{
    public HudLayout Layout { get; }
    public HudSection ListSection { get; }

    public CraftingRenderer()
    {
        Layout = new HudLayout();
        Layout.AddSection(new HudSection { Name = "CraftHeader", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 3 });
        ListSection = new HudSection { Name = "CraftList", Anchor = HudAnchor.Top, IsFixedHeight = false, Scrollable = true, AcceptsInput = true, UseScrollIndicators = true };
        Layout.AddSection(ListSection);
        Layout.AddSection(new HudSection { Name = "CraftDetail", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 14 });
        Layout.AddSection(new HudSection { Name = "CraftActions", Anchor = HudAnchor.Bottom, IsFixedHeight = true, FixedHeight = 4 });
        Layout.SetFocus(1);
    }

    public void Render(
        ISpriteRenderer r,
        PlayerStateMsg? playerState,
        bool inCategoryMode,
        int selectedInternalCategoryId,
        int[] internalCategoryIdsInOrder,
        RecipeDefinition[] filteredRecipes,
        IReadOnlyList<int> recentRecipeIds,
        bool debugFreeCraft,
        int hudStartCol,
        int totalRows)
    {
        HudPanelChrome.DrawBorder(r, hudStartCol, totalRows);

        int col = hudStartCol + 1;
        int innerW = AsciiDraw.HudColumns - 1;

        if (playerState == null)
        {
            AsciiDraw.DrawString(r, col, 1, "No data", RenderingTheme.Dim);
            return;
        }

        Layout.ComputeLayout(totalRows);

        foreach (var section in Layout.Sections)
        {
            int row = section.StartRow;
            int maxRow = section.StartRow + section.RowCount;

            switch (section.Name)
            {
                case "CraftHeader":
                    if (row >= maxRow) break;
                    string title = inCategoryMode ? "CRAFTING" : $"CRAFTING > {InternalCategoryName(selectedInternalCategoryId)}";
                    if (title.Length > innerW) title = title[..innerW];
                    AsciiDraw.DrawString(r, col, row, title, RenderingTheme.Title);
                    AsciiDraw.DrawString(r, col + innerW - 5, row, "[ESC]", RenderingTheme.Dim);
                    row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW);
                    break;

                case "CraftList":
                    if (inCategoryMode)
                        RenderCategoryList(r, col, innerW, row, maxRow, playerState, section, internalCategoryIdsInOrder, debugFreeCraft);
                    else
                        RenderRecipesSection(r, col, innerW, row, maxRow, playerState, section, filteredRecipes, debugFreeCraft);
                    break;

                case "CraftDetail":
                    if (!inCategoryMode)
                        RenderDetailSection(r, col, innerW, row, maxRow, playerState, filteredRecipes, debugFreeCraft);
                    else
                        RenderRecentRecipes(r, col, innerW, row, maxRow, playerState, recentRecipeIds, debugFreeCraft);
                    break;

                case "CraftActions":
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;
                    if (inCategoryMode)
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
        PlayerStateMsg playerState, HudSection section, int[] internalCategoryIdsInOrder, bool debugFreeCraft)
    {
        int count = internalCategoryIdsInOrder.Length;
        int scrollOffset = section.ScrollOffset;
        int selectedIndex = section.SelectedIndex;
        int visibleRows = maxRow - row;

        bool showTopArrow = scrollOffset > 0;
        bool showBottomArrow = scrollOffset + visibleRows < count;

        int renderEnd = Math.Min(scrollOffset + visibleRows, count);
        for (int i = scrollOffset; i < renderEnd && row < maxRow; i++)
        {
            int cat = internalCategoryIdsInOrder[i];
            bool hasCraftable = CategoryHasCraftable(cat, playerState, debugFreeCraft);
            bool sel = i == selectedIndex;

            string prefix = sel ? "\u25ba" : " ";
            int totalCount = CountRecipesInCategory(cat);
            int craftableCount = CountCraftableInCategory(cat, playerState, debugFreeCraft);
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

    private static void RenderRecipesSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg playerState, HudSection section, RecipeDefinition[] filteredRecipes, bool debugFreeCraft)
    {
        int recipeCount = filteredRecipes.Length;
        int scrollOffset = section.ScrollOffset;
        int selectedIndex = section.SelectedIndex;
        int visibleRows = maxRow - row;

        bool showTopArrow = scrollOffset > 0;
        bool showBottomArrow = scrollOffset + visibleRows < recipeCount;

        int renderEnd = Math.Min(scrollOffset + visibleRows, recipeCount);
        for (int i = scrollOffset; i < renderEnd && row < maxRow; i++)
        {
            var recipe = filteredRecipes[i];
            bool canCraft = CanCraftRecipe(recipe, playerState, debugFreeCraft);
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

    private static void RenderRecentRecipes(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg playerState, IReadOnlyList<int> recentRecipeIds, bool debugFreeCraft)
    {
        if (recentRecipeIds.Count == 0 || row >= maxRow) return;

        AsciiDraw.DrawString(r, col, row, "Recent:", RenderingTheme.Dim); row++;
        if (row >= maxRow) return;
        AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;

        var recipes = GameData.Instance.Recipes;
        foreach (var recipeId in recentRecipeIds)
        {
            if (row >= maxRow) break;
            var recipe = recipes.Get(recipeId);
            if (recipe == null) continue;
            bool canCraft = CanCraftRecipe(recipe, playerState, debugFreeCraft);
            var def = GameData.Instance.Items.Get(recipe.Result.NumericItemId);
            string tag = def != null ? AsciiDraw.CategoryTag(def.CategoryInt) : "     ";
            string text = $"  {tag}{recipe.Name}";
            if (text.Length > innerW) text = text[..innerW];
            AsciiDraw.DrawString(r, col, row, text, canCraft ? RenderingTheme.Item : RenderingTheme.Dim);
            row++;
        }
    }

    private void RenderDetailSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg playerState, RecipeDefinition[] filteredRecipes, bool debugFreeCraft)
    {
        if (row >= maxRow) return;
        AsciiDraw.DrawString(r, col, row, "Ingredients:", RenderingTheme.Dim); row++;
        if (row >= maxRow) return;
        AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;

        int selIdx = ListSection.SelectedIndex;
        if (selIdx < 0 || selIdx >= filteredRecipes.Length) return;

        var recipe = filteredRecipes[selIdx];
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
            bool canCraft = CanCraftRecipe(recipe, playerState, debugFreeCraft);
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

    // ─── Domain helpers (shared with CraftingScreen for TryCraft/ordering) ─────

    public static bool CanCraftRecipe(RecipeDefinition recipe, PlayerStateMsg playerState, bool debugFreeCraft = false)
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

    public static bool HasNearbyStation(RecipeDefinition recipe, PlayerStateMsg playerState)
    {
        int stationId = (int)recipe.Station;
        foreach (var s in playerState.NearbyStationsTypes)
        {
            if (s == stationId) return true;
        }
        return false;
    }

    public static string StationName(CraftingStationType station) => station switch
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

    public static int CountItem(PlayerStateMsg playerState, int itemTypeId)
    {
        int count = 0;
        foreach (var item in playerState.InventoryItems)
        {
            if (item.ItemTypeId == itemTypeId)
                count += item.StackCount;
        }
        return count;
    }

    public static bool CategoryHasCraftable(int category, PlayerStateMsg playerState, bool debugFreeCraft)
    {
        foreach (var r in GameData.Instance.Recipes.All)
        {
            var def = GameData.Instance.Items.Get(r.Result.NumericItemId);
            if (def != null && InternalCategoryId(def) == category && CanCraftRecipe(r, playerState, debugFreeCraft))
                return true;
        }
        return false;
    }

    public static int CountRecipesInCategory(int category)
    {
        int count = 0;
        foreach (var r in GameData.Instance.Recipes.All)
        {
            var def = GameData.Instance.Items.Get(r.Result.NumericItemId);
            if (def != null && InternalCategoryId(def) == category) count++;
        }
        return count;
    }

    public static int CountCraftableInCategory(int category, PlayerStateMsg playerState, bool debugFreeCraft)
    {
        int count = 0;
        foreach (var r in GameData.Instance.Recipes.All)
        {
            var def = GameData.Instance.Items.Get(r.Result.NumericItemId);
            if (def != null && InternalCategoryId(def) == category && CanCraftRecipe(r, playerState, debugFreeCraft)) count++;
        }
        return count;
    }

    /// <summary>
    /// Returns an effective category ID for filtering.
    /// Placeable items return a sub-category ID encoding their PlaceableType
    /// (PlaceableCategoryBase + PlaceableType). All other items return CategoryInt.
    /// </summary>
    public static int InternalCategoryId(ItemDefinition itemDefinition) =>
        itemDefinition.Category == ItemCategory.Placeable && itemDefinition.Placeable != null
            ? InternalPlaceableCategoryId(itemDefinition.Placeable.PlaceableType)
            : itemDefinition.CategoryInt;

    /// <summary>Base value for placeable sub-category IDs (PlaceableCategoryBase + PlaceableType).</summary>
    private const int PlaceableCategoryBase = 1000;

    private static int InternalPlaceableCategoryId(PlaceableType pt) => PlaceableCategoryBase + (int)pt;

    private static bool IsPlaceableSubCategory(int categoryId) => categoryId >= PlaceableCategoryBase;

    public static string InternalCategoryName(int categoryId) => IsPlaceableSubCategory(categoryId)
        ? ItemDefinition.PlaceableTypeName((PlaceableType)(categoryId - PlaceableCategoryBase))
        : ItemDefinition.CategoryName((ItemCategory)categoryId);
}
