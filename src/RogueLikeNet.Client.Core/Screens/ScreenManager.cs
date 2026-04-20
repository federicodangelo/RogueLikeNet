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
    private readonly OptionsScreen? _options;
    private readonly MainMenuScreen? _mainMenu;
    private readonly InventoryScreen? _inventory;
    private readonly CraftingScreen? _crafting;
    private readonly SaveSlotScreen? _saveSlot;
    private readonly ServerAdminScreen? _serverAdmin;
    private readonly NewGameScreen? _newGame;
    private readonly LoginScreen? _login;
    private readonly ShopScreen? _shop;

    public ScreenState CurrentState { get; private set; }

    public ScreenManager(
        MainMenuScreen mainMenu,
        ClassSelectScreen classSelect,
        ConnectingScreen connecting,
        PlayingScreen playing,
        InventoryScreen inventory,
        CraftingScreen crafting,
        PausedScreen paused,
        HelpScreen help,
        OptionsScreen options,
        SaveSlotScreen saveSlot,
        ServerAdminScreen serverAdmin,
        NewGameScreen newGame,
        LoginScreen login,
        ShopScreen shop)
    {
        _mainMenu = mainMenu;
        _classSelect = classSelect;
        _connecting = connecting;
        _paused = paused;
        _help = help;
        _options = options;
        _inventory = inventory;
        _crafting = crafting;
        _saveSlot = saveSlot;
        _serverAdmin = serverAdmin;
        _newGame = newGame;
        _login = login;
        _shop = shop;

        _screens[ScreenState.MainMenu] = mainMenu;
        _screens[ScreenState.ClassSelect] = classSelect;
        _screens[ScreenState.SaveSlotSelect] = saveSlot;
        _screens[ScreenState.Connecting] = connecting;
        _screens[ScreenState.Playing] = playing;
        _screens[ScreenState.Inventory] = inventory;
        _screens[ScreenState.Crafting] = crafting;
        _screens[ScreenState.Paused] = paused;
        _screens[ScreenState.MainMenuHelp] = help;
        _screens[ScreenState.PausedHelp] = help;
        _screens[ScreenState.Options] = options;
        _screens[ScreenState.PausedOptions] = options;
        _screens[ScreenState.ServerAdmin] = serverAdmin;
        _screens[ScreenState.NewGame] = newGame;
        _screens[ScreenState.Login] = login;
        _screens[ScreenState.Shop] = shop;

        _current = mainMenu;
        CurrentState = ScreenState.MainMenu;
    }

    public void TransitionTo(ScreenState state)
    {
        var previousState = CurrentState;

        if (_screens.TryGetValue(state, out var screen))
        {
            _current = screen;
            CurrentState = state;
        }

        // Run enter hooks after state is set so synchronous callbacks see the correct CurrentState
        switch (state)
        {
            case ScreenState.ClassSelect:
                _classSelect?.OnEnter();
                break;
            case ScreenState.SaveSlotSelect:
                _saveSlot?.OnEnter();
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
            case ScreenState.Options:
                _options?.SetMode(ScreenState.Options, ScreenState.MainMenu);
                break;
            case ScreenState.PausedOptions:
                _options?.SetMode(ScreenState.PausedOptions, ScreenState.Paused);
                break;
            case ScreenState.Inventory:
                // If coming from crafting, select the last crafted item
                if (previousState == ScreenState.Crafting && _crafting != null && _crafting.LastCraftedItemTypeId != 0)
                    _inventory?.SetSelectItemOnEnter(_crafting.LastCraftedItemTypeId);
                _inventory?.OnEnter();
                break;
            case ScreenState.Crafting:
                _crafting?.OnEnter();
                break;
            case ScreenState.ServerAdmin:
                _serverAdmin?.OnEnter();
                break;
            case ScreenState.NewGame:
                _newGame?.OnEnter();
                break;
            case ScreenState.Login:
                _login?.OnEnter();
                break;
            case ScreenState.Shop:
                _shop?.OnEnter();
                break;
        }
    }

    public void OpenShop(RogueLikeNet.Core.Data.TownNpcRole role)
    {
        _shop?.OpenShop(role);
        TransitionTo(ScreenState.Shop);
    }

    public void HandleInput(IInputManager input) => _current.HandleInput(input);
    public void Update(float deltaTime) => _current.Update(deltaTime);
    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        using var _ = new TimeMeasurer("ScreenManager.Render");
        _current.Render(renderer, totalCols, totalRows);
    }
}
