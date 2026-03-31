using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Class selection screen — browse classes, edit player name, confirm selection.
/// </summary>
public sealed class ClassSelectScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly MenuRenderer _menuRenderer;
    private readonly MainMenuScreen _mainMenuScreen;

    private int _selectedClassIndex;
    private string _playerName = "Hero";
    private bool _nameEditing;
    private string _nameEditText = "";
    private bool _isOnline;
    private ScreenState _returnState = ScreenState.MainMenu;

    public ScreenState ScreenState => ScreenState.ClassSelect;

    public ClassSelectScreen(ScreenContext ctx, MenuRenderer menuRenderer, MainMenuScreen mainMenuScreen)
    {
        _ctx = ctx;
        _menuRenderer = menuRenderer;
        _mainMenuScreen = mainMenuScreen;
    }

    public void OnEnter()
    {
        _selectedClassIndex = 0;
        _isOnline = _mainMenuScreen.IsOnlineSelected;
    }

    public void SetReturnState(ScreenState state) => _returnState = state;

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
            _ctx.RequestTransition(_returnState);
            _returnState = ScreenState.MainMenu;
            return;
        }

        if (input.IsActionPressed(InputAction.MoveLeft) || input.IsActionPressed(InputAction.MenuUp))
            _selectedClassIndex = (_selectedClassIndex + classCount - 1) % classCount;
        else if (input.IsActionPressed(InputAction.MoveRight) || input.IsActionPressed(InputAction.MenuDown))
            _selectedClassIndex = (_selectedClassIndex + 1) % classCount;
        else if (input.IsActionPressed(InputAction.OpenChat))
        {
            _nameEditing = true;
            _nameEditText = _playerName;
        }
        else if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            int classId = ClassDefinitions.All[_selectedClassIndex].ClassId;
            if (_isOnline)
                _ctx.OnStartOnline(classId, _playerName);
            else
                _ctx.OnStartOffline(_mainMenuScreen.WorldSeed, classId, _playerName, _mainMenuScreen.GeneratorIndex);
        }
    }

    public void Update(float deltaTime) { }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        _menuRenderer.RenderClassSelect(renderer, totalCols, totalRows, _selectedClassIndex, _playerName, _nameEditing, _nameEditText);
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
