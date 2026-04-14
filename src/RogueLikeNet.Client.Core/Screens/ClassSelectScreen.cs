using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Class selection screen — browse classes, edit player name, confirm selection.
/// </summary>
public sealed class ClassSelectScreen : IScreen
{
    public const string DefaultPlayerName = "Hero";

    private readonly ScreenContext _ctx;
    private readonly MenuRenderer _menuRenderer;
    private readonly MainMenuScreen _mainMenuScreen;
    private readonly NewGameScreen _newGameScreen;

    private int _selectedClassIndex;
    private string _playerName = DefaultPlayerName;
    private bool _nameEditing;
    private string _nameEditText = "";
    private bool _isOnline;
    private bool _canEditName;

    public ScreenState ScreenState => ScreenState.ClassSelect;

    public ClassSelectScreen(ScreenContext ctx, MenuRenderer menuRenderer, MainMenuScreen mainMenuScreen, NewGameScreen newGameScreen)
    {
        _ctx = ctx;
        _menuRenderer = menuRenderer;
        _mainMenuScreen = mainMenuScreen;
        _newGameScreen = newGameScreen;
    }

    public void OnEnter()
    {
        _selectedClassIndex = 0;
        _isOnline = _mainMenuScreen.IsOnlineSelected;
        if (!_isOnline)
            _playerName = DefaultPlayerName; // For offline mode, reset to default name to match the name in the savegame
        _canEditName = _isOnline; // Only allow editing name for online mode
    }

    public void HandleInput(IInputManager input)
    {
        if (_nameEditing)
        {
            HandleNameEditing(input);
            return;
        }

        int classCount = ClassDefinitions.All.Length;

        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _ctx.OnReturnToMenu();
            return;
        }

        if (input.IsActionPressedOrRepeated(InputAction.MoveLeft) || input.IsActionPressedOrRepeated(InputAction.MenuUp))
            _selectedClassIndex = (_selectedClassIndex + classCount - 1) % classCount;
        else if (input.IsActionPressedOrRepeated(InputAction.MoveRight) || input.IsActionPressedOrRepeated(InputAction.MenuDown))
            _selectedClassIndex = (_selectedClassIndex + 1) % classCount;
        else if (input.IsActionPressed(InputAction.OpenChat) && _canEditName)
        {
            _nameEditing = true;
            _nameEditText = _playerName;
        }
        else if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            int classId = _selectedClassIndex;
            if (_isOnline)
                _ctx.OnStartOnline(classId, _playerName);
            else
                _ctx.OnStartOffline(_newGameScreen.WorldSeed, classId, _playerName, _newGameScreen.GeneratorIndex);
        }
    }

    public void Update(float deltaTime) { }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        _menuRenderer.RenderClassSelect(renderer, totalCols, totalRows, _selectedClassIndex, _playerName, _nameEditing, _nameEditText, _canEditName);
    }

    private void HandleNameEditing(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _nameEditing = false;
            return;
        }

        for (int i = 0; i < input.TextInputBackspacesCount; i++)
        {
            if (_nameEditText.Length > 0)
                _nameEditText = _nameEditText[..^1];
        }

        if (input.TextInputReturnsCount > 0)
        {
            if (_nameEditText.Length > 0)
                _playerName = _nameEditText;
            _nameEditing = false;
            return;
        }

        string typed = input.TextInput;
        foreach (char c in typed)
        {
            if ((char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-') && _nameEditText.Length < 16)
                _nameEditText += c;
        }
    }
}
