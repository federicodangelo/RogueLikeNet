using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Core.Utilities;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Manages screen transitions and dispatches input/update/render to the active screen.
/// </summary>
public sealed class ScreenManager
{
    private readonly Dictionary<ScreenState, IScreen> _screens = new();
    private IScreen _current = null!;

    // Concrete screen references for transition hooks
    private readonly ClassSelectScreen? _classSelect;
    private readonly ConnectingScreen? _connecting;
    private readonly PausedScreen? _paused;
    private readonly HelpScreen? _help;
    private readonly MainMenuScreen? _mainMenu;
    private readonly InventoryScreen? _inventory;

    public ScreenState CurrentState { get; private set; }

    public ScreenManager(
        MainMenuScreen mainMenu,
        ClassSelectScreen classSelect,
        ConnectingScreen connecting,
        PlayingScreen playing,
        InventoryScreen inventory,
        PausedScreen paused,
        HelpScreen help)
    {
        _mainMenu = mainMenu;
        _classSelect = classSelect;
        _connecting = connecting;
        _paused = paused;
        _help = help;
        _inventory = inventory;

        _screens[ScreenState.MainMenu] = mainMenu;
        _screens[ScreenState.ClassSelect] = classSelect;
        _screens[ScreenState.Connecting] = connecting;
        _screens[ScreenState.Playing] = playing;
        _screens[ScreenState.Inventory] = inventory;
        _screens[ScreenState.Paused] = paused;
        _screens[ScreenState.MainMenuHelp] = help;
        _screens[ScreenState.PausedHelp] = help;

        _current = mainMenu;
        CurrentState = ScreenState.MainMenu;
    }

    public void TransitionTo(ScreenState state)
    {
        // Run enter hooks for specific screens
        switch (state)
        {
            case ScreenState.ClassSelect:
                _classSelect?.OnEnter();
                break;
            case ScreenState.Paused:
                _paused?.OnEnter();
                break;
            case ScreenState.MainMenu:
                _mainMenu?.ResetMenuIndex();
                break;
            case ScreenState.MainMenuHelp:
                _help?.SetMode(ScreenState.MainMenuHelp, ScreenState.MainMenu);
                break;
            case ScreenState.PausedHelp:
                _help?.SetMode(ScreenState.PausedHelp, ScreenState.Paused);
                break;
            case ScreenState.Inventory:
                _inventory?.OnEnter();
                break;
        }

        if (_screens.TryGetValue(state, out var screen))
        {
            _current = screen;
            CurrentState = state;
        }
    }

    public void HandleInput(IInputManager input) => _current.HandleInput(input);
    public void Update(float deltaTime) => _current.Update(deltaTime);
    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        using var _ = new TimeMeasurer("ScreenManager.Render");
        _current.Render(renderer, totalCols, totalRows);
    }
}
