using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.Rendering.Menus;

namespace RogueLikeNet.Client.Core.Screens.Playing;

/// <summary>
/// Options screen — toggle game settings. Accessible from both main menu and pause menu.
/// Debug mode toggle is only available when accessed from the main menu.
/// </summary>
public sealed class OptionsScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly OptionsRenderer _menuRenderer;
    private readonly PlayingScreen? _playingScreen;

    private ScreenState _screenState;
    private ScreenState _returnTo;
    private int _menuIndex;

    public ScreenState ScreenState => _screenState;

    /// <summary>Whether debug mode option is visible (only when accessed from main menu).</summary>
    private bool ShowDebugOption => _returnTo == ScreenState.MainMenu;

    private int ItemCount => ShowDebugOption ? 3 : 2;

    public OptionsScreen(ScreenContext ctx, OptionsRenderer menuRenderer, PlayingScreen? playingScreen)
    {
        _ctx = ctx;
        _menuRenderer = menuRenderer;
        _playingScreen = playingScreen;
    }

    public void SetMode(ScreenState optionsState, ScreenState returnTo)
    {
        _screenState = optionsState;
        _returnTo = returnTo;
        _menuIndex = 0;
    }

    public void HandleInput(IInputManager input)
    {
        int itemCount = ItemCount;

        if (input.IsActionPressed(InputAction.MenuBack))
        {
            SaveOptions();
            _ctx.RequestTransition(_returnTo);
            return;
        }

        InputHelpers.HandleListNavigation(input, ref _menuIndex, itemCount);

        if (input.IsActionPressed(InputAction.MenuConfirm) ||
            input.IsActionPressed(InputAction.MoveLeft) ||
            input.IsActionPressed(InputAction.MoveRight))
        {
            ToggleCurrentItem();
        }
    }

    public void Update(float deltaTime) { }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        bool isOverlay = _returnTo == ScreenState.Paused;

        if (isOverlay && _playingScreen != null)
            _playingScreen.Render(renderer, totalCols, totalRows);

        _menuRenderer.RenderOptions(renderer, totalCols, totalRows, _menuIndex,
            _ctx.Options.ShowStats, _ctx.Options.ShowQuestTracker, ShowDebugOption, _ctx.Debug.Enabled, isOverlay);
    }

    private void ToggleCurrentItem()
    {
        if (_menuIndex == 0)
        {
            _ctx.Options.ShowStats = !_ctx.Options.ShowStats;
        }
        else if (_menuIndex == 1)
        {
            _ctx.Options.ShowQuestTracker = !_ctx.Options.ShowQuestTracker;
        }
        else if (_menuIndex == 2 && ShowDebugOption)
        {
            _ctx.Debug.Enabled = !_ctx.Debug.Enabled;
        }
    }

    private void SaveOptions()
    {
        var settings = _ctx.Settings;
        if (settings == null) return;
        _ctx.Options.Save(settings);
        if (ShowDebugOption)
            _ctx.Options.SaveDebugEnabled(settings, _ctx.Debug);
    }
}
