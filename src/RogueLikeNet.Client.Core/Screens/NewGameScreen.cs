using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// New game configuration screen — seed, generator, world name setup.
/// Shared by SaveSlotScreen (offline) and ServerAdminScreen (online admin).
/// </summary>
public sealed class NewGameScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly MenuRenderer _menuRenderer;

    private int _menuIndex;
    private long _worldSeed = Random.Shared.NextInt64(0, 1_000_000_000);
    private int _generatorIndex = GeneratorRegistry.DefaultIndex;
    private bool _seedEditing;
    private string _seedEditText = "";
    private string _slotName = "";
    private bool _nameEditing;
    private string _nameEditText = "";
    private ScreenState _returnState;

    public long WorldSeed => _worldSeed;
    public int GeneratorIndex => _generatorIndex;
    public ScreenState ScreenState => ScreenState.NewGame;

    /// <summary>Fired when user confirms the new game (slot name).</summary>
    public Action<string>? OnConfirmed;

    private const int MenuName = 0;
    private const int MenuSeed = 1;
    private const int MenuGenerator = 2;
    private const int MenuRandomize = 3;
    private const int MenuStart = 4;
    private const int MenuItemCount = 5;

    public NewGameScreen(ScreenContext ctx, MenuRenderer menuRenderer)
    {
        _ctx = ctx;
        _menuRenderer = menuRenderer;
    }

    public void SetReturnState(ScreenState returnState)
    {
        _returnState = returnState;
    }

    public void OnEnter()
    {
        _menuIndex = 0;
        _seedEditing = false;
        _nameEditing = false;
        _slotName = "";
        _nameEditText = "";
    }

    public void HandleInput(IInputManager input)
    {
        if (_seedEditing) { HandleSeedEditing(input); return; }
        if (_nameEditing) { HandleNameEditing(input); return; }

        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _ctx.RequestTransition(_returnState);
            return;
        }

        if (input.IsActionPressedOrRepeated(InputAction.MenuUp))
            _menuIndex = (_menuIndex + MenuItemCount - 1) % MenuItemCount;
        else if (input.IsActionPressedOrRepeated(InputAction.MenuDown))
            _menuIndex = (_menuIndex + 1) % MenuItemCount;

        if (_menuIndex == MenuGenerator)
        {
            int genCount = GeneratorRegistry.Count;
            if (input.IsActionPressedOrRepeated(InputAction.MoveLeft))
                _generatorIndex = (_generatorIndex + genCount - 1) % genCount;
            else if (input.IsActionPressedOrRepeated(InputAction.MoveRight))
                _generatorIndex = (_generatorIndex + 1) % genCount;
        }
        else if (_menuIndex == MenuSeed)
        {
            if (input.IsActionPressedOrRepeated(InputAction.MoveLeft) && _worldSeed > 0)
                _worldSeed--;
            else if (input.IsActionPressedOrRepeated(InputAction.MoveRight) && _worldSeed < long.MaxValue)
                _worldSeed++;
        }

        if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            switch (_menuIndex)
            {
                case MenuName:
                    _nameEditing = true;
                    _nameEditText = _slotName;
                    break;
                case MenuSeed:
                    _seedEditing = true;
                    _seedEditText = _worldSeed.ToString();
                    break;
                case MenuGenerator: break;
                case MenuRandomize:
                    _worldSeed = Random.Shared.NextInt64(0, 1_000_000_000);
                    break;
                case MenuStart:
                    if (_slotName.Length > 0)
                        OnConfirmed?.Invoke(_slotName);
                    break;
            }
        }
    }

    public void Update(float deltaTime) { }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        _menuRenderer.RenderNewGame(renderer, totalCols, totalRows, _menuIndex,
            _slotName, _worldSeed, _generatorIndex, _seedEditing, _seedEditText,
            _nameEditing, _nameEditText);
    }

    private void HandleSeedEditing(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _seedEditing = false;
            return;
        }

        for (int i = 0; i < input.TextInputBackspacesCount; i++)
        {
            if (_seedEditText.Length > 0)
                _seedEditText = _seedEditText[..^1];
        }

        if (input.TextInputReturnsCount > 0)
        {
            if (long.TryParse(_seedEditText, out long parsed))
                _worldSeed = parsed;
            _seedEditing = false;
            return;
        }

        string typed = input.TextInput;
        foreach (char c in typed)
        {
            if (char.IsAsciiDigit(c) && _seedEditText.Length < 18)
                _seedEditText += c;
        }
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
                _slotName = _nameEditText;
            _nameEditing = false;
            return;
        }

        string typed = input.TextInput;
        foreach (char c in typed)
        {
            if ((char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '\'') && _nameEditText.Length < 24)
                _nameEditText += c;
        }
    }
}
