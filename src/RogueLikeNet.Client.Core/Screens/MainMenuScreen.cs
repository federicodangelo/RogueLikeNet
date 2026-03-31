using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Main menu screen — play offline/online, seed editing, help, quit.
/// </summary>
public sealed class MainMenuScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly MenuRenderer _menuRenderer;

    private int _menuIndex;
    private long _worldSeed = Random.Shared.NextInt64(0, 1_000_000_000);
    private int _generatorIndex = GeneratorRegistry.DefaultIndex;
    private bool _seedEditing;
    private string _seedEditText = "";

    public ScreenState ScreenState => ScreenState.MainMenu;
    public long WorldSeed => _worldSeed;
    public int GeneratorIndex => _generatorIndex;

    public MainMenuScreen(ScreenContext ctx, MenuRenderer menuRenderer)
    {
        _ctx = ctx;
        _menuRenderer = menuRenderer;
    }

    public void HandleInput(IInputManager input)
    {
        if (_seedEditing)
        {
            HandleSeedEditing(input);
            return;
        }

        int itemCount = 8;
        if (input.IsActionPressed(InputAction.MenuUp))
            _menuIndex = (_menuIndex + itemCount - 1) % itemCount;
        else if (input.IsActionPressed(InputAction.MenuDown))
            _menuIndex = (_menuIndex + 1) % itemCount;

        // Left/right arrows cycle the generator when on the Generator row
        if (_menuIndex == MenuRenderer.MainMenuGeneratorIndex)
        {
            int genCount = GeneratorRegistry.Count;
            if (input.IsActionPressed(InputAction.MoveLeft))
                _generatorIndex = (_generatorIndex + genCount - 1) % genCount;
            else if (input.IsActionPressed(InputAction.MoveRight))
                _generatorIndex = (_generatorIndex + 1) % genCount;
        }
        else if (_menuIndex == MenuRenderer.MainMenuSeedIndex) // Don't allow up/down to change seed when on Seed row (to avoid accidental changes)
        {
            if (input.IsActionPressed(InputAction.MoveLeft) && _worldSeed > 0)
                _worldSeed--;
            else if (input.IsActionPressed(InputAction.MoveRight) && _worldSeed < long.MaxValue)
                _worldSeed++;
        }
        else if (_menuIndex == MenuRenderer.MainMenuDebugModeIndex)
        {
            if (input.IsActionPressed(InputAction.MoveLeft) || input.IsActionPressed(InputAction.MoveRight))
                _ctx.Debug.Enabled = !_ctx.Debug.Enabled;
        }

        if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            switch (_menuIndex)
            {
                case MenuRenderer.MainMenuPlayOfflineIndex:
                    SetClassSelectOnline(false);
                    _ctx.OnPlayOffline();
                    break;
                case MenuRenderer.MainMenuPlayOnlineIndex:
                    _ctx.RequestTransition(Rendering.ScreenState.ClassSelect);
                    SetClassSelectOnline(true);
                    break;
                case MenuRenderer.MainMenuSeedIndex:
                    _seedEditing = true;
                    _seedEditText = _worldSeed.ToString();
                    break;
                case MenuRenderer.MainMenuGeneratorIndex: break; // Generator — left/right only, no action on Enter
                case MenuRenderer.MainMenuRandomizeSeedIndex: _worldSeed = Random.Shared.NextInt64(0, 1_000_000_000); break;
                case MenuRenderer.MainMenuDebugModeIndex: _ctx.Debug.Enabled = !_ctx.Debug.Enabled; break;
                case MenuRenderer.MainMenuHelpIndex: _ctx.RequestTransition(Rendering.ScreenState.MainMenuHelp); break;
                case MenuRenderer.MainMenuQuitIndex: _ctx.OnQuit(); break;
            }
        }
    }

    public void Update(float deltaTime) { }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        _menuRenderer.RenderMainMenu(renderer, totalCols, totalRows, _menuIndex, _worldSeed, _generatorIndex, _seedEditing, _seedEditText, _ctx.Debug.Enabled);
    }

    public void ResetMenuIndex()
    {
        _menuIndex = 0;
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

    // Bridge to ClassSelectScreen — stored here temporarily until transition
    internal bool IsOnlineSelected { get; private set; }
    private void SetClassSelectOnline(bool online) => IsOnlineSelected = online;
}
