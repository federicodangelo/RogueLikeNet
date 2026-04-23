using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.Rendering.Hud;
using RogueLikeNet.Client.Core.Rendering.Overlays;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens.Playing;

/// <summary>
/// Crafting screen — category selection first, then recipe list, with recent recipes, key repeat, and smart sorting.
/// Rendering is delegated to <see cref="CraftingRenderer"/>.
/// </summary>
public sealed class CraftingScreen : PlayingOverlayScreen
{
    private readonly CraftingRenderer _craftingRenderer;

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

    public override ScreenState ScreenState => ScreenState.Crafting;

    private bool IsDebugFreeCraft => _ctx.Debug is { Enabled: true, FreeCrafting: true };

    public CraftingScreen(ScreenContext ctx, PlayingBackdropRenderer backdrop,
        CraftingRenderer craftingRenderer, OverlayRenderer overlayRenderer)
        : base(ctx, backdrop, overlayRenderer)
    {
        _craftingRenderer = craftingRenderer;
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
        _craftingRenderer.ListSection.SelectedIndex = 0;
        _craftingRenderer.ListSection.ScrollOffset = 0;
        RebuildCategoryOrder();
    }

    protected override void OnLeavingViaSharedNavigation(ScreenState target)
    {
        // Preserve selection + scroll state when cross-navigating between HUD panel screens.
        _preserveStateOnEnter = true;
    }

    public override void HandleInput(IInputManager input)
    {
        var listSection = _craftingRenderer.ListSection;

        // Sub-mode MenuBack: exit recipe list back to category list before any shared navigation.
        if (!_inCategoryMode && input.IsActionPressed(InputAction.MenuBack))
        {
            _inCategoryMode = true;
            RebuildCategoryOrder();
            listSection.SelectedIndex = _savedCategoryIndex;
            listSection.ScrollOffset = _savedCategoryScroll;
            return;
        }

        if (TryHandleSharedNavigation(input)) return;

        if (_inCategoryMode)
        {
            int count = _internalCategoryIdsInOrder.Length;
            if (input.IsActionPressedOrRepeated(InputAction.MenuUp)) listSection.ScrollUp();
            else if (input.IsActionPressedOrRepeated(InputAction.MenuDown)) listSection.ScrollDown(count);
            else if (input.IsActionPressed(InputAction.MenuConfirm))
            {
                int idx = listSection.SelectedIndex;
                if (idx >= 0 && idx < _internalCategoryIdsInOrder.Length)
                {
                    _selectedInternalCategoryId = _internalCategoryIdsInOrder[idx];
                    _savedCategoryIndex = idx;
                    _savedCategoryScroll = listSection.ScrollOffset;
                    _inCategoryMode = false;
                    RebuildFilteredRecipes();
                    listSection.SelectedIndex = 0;
                    listSection.ScrollOffset = 0;
                }
            }
        }
        else
        {
            int count = _filteredRecipes.Length;
            if (input.IsActionPressedOrRepeated(InputAction.MenuUp)) listSection.ScrollUp();
            else if (input.IsActionPressedOrRepeated(InputAction.MenuDown)) listSection.ScrollDown(count);
            else if (input.IsActionPressed(InputAction.MenuConfirm))
                TryCraft();
        }
    }

    protected override void RenderPanel(ISpriteRenderer renderer, int hudStartCol, int totalRows)
    {
        _craftingRenderer.Render(renderer, _ctx.GameState.PlayerState,
            _inCategoryMode, _selectedInternalCategoryId,
            _internalCategoryIdsInOrder, _filteredRecipes, _recentRecipeIds,
            IsDebugFreeCraft, hudStartCol, totalRows);
    }

    private void TryCraft()
    {
        if (_ctx.Connection == null) return;
        int selIdx = _craftingRenderer.ListSection.SelectedIndex;
        if (selIdx < 0 || selIdx >= _filteredRecipes.Length) return;

        var hud = _ctx.GameState.PlayerState;
        if (hud == null) return;

        var recipe = _filteredRecipes[selIdx];
        bool debugFree = IsDebugFreeCraft;
        if (!debugFree && !CraftingRenderer.CanCraftRecipe(recipe, hud)) return;

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
            if (def != null) categories.Add(CraftingRenderer.InternalCategoryId(def));
        }

        // Sort: categories with craftable recipes first, then by category Name
        bool debugFree = IsDebugFreeCraft;
        var sorted = categories.ToList();
        sorted.Sort((a, b) =>
        {
            bool aCraft = playerState != null && CraftingRenderer.CategoryHasCraftable(a, playerState, debugFree);
            bool bCraft = playerState != null && CraftingRenderer.CategoryHasCraftable(b, playerState, debugFree);
            if (aCraft != bCraft) return aCraft ? -1 : 1;
            var aName = CraftingRenderer.InternalCategoryName(a);
            var bName = CraftingRenderer.InternalCategoryName(b);
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
            if (def != null && CraftingRenderer.InternalCategoryId(def) == _selectedInternalCategoryId)
                filtered.Add(r);
        }

        // Sort: craftable first, then by name
        bool debugFree = IsDebugFreeCraft;
        filtered.Sort((a, b) =>
        {
            bool aCraft = hud != null && CraftingRenderer.CanCraftRecipe(a, hud, debugFree);
            bool bCraft = hud != null && CraftingRenderer.CanCraftRecipe(b, hud, debugFree);
            if (aCraft != bCraft) return aCraft ? -1 : 1;
            return string.Compare(a.Name, b.Name, StringComparison.InvariantCultureIgnoreCase);
        });
        _filteredRecipes = filtered.ToArray();
    }
}
