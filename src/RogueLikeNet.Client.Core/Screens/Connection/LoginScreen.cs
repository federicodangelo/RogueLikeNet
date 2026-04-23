using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.Rendering.Menus;

namespace RogueLikeNet.Client.Core.Screens.Connection;

/// <summary>
/// Login screen — enter username and password before connecting to an online server.
/// Fields are directly editable without needing to press Enter first.
/// </summary>
public sealed class LoginScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly LoginRenderer _menuRenderer;

    private string _userName = "";
    private string _password = "";
    private int _selectedField; // 0 = username, 1 = password
    private string _editText = "";
    private string? _errorMessage;
    private bool _clearErrorMessageOnNextEnter = true;
    private bool _loadedUsername;

    public ScreenState ScreenState => ScreenState.Login;

    /// <summary>The player name entered by the user (available after login).</summary>
    public string UserName => _userName;

    /// <summary>The password entered by the user (available after login).</summary>
    public string Password => _password;

    public LoginScreen(ScreenContext ctx, LoginRenderer menuRenderer)
    {
        _ctx = ctx;
        _menuRenderer = menuRenderer;
    }

    private bool _firstTime = true;

    public void OnEnter()
    {
        if (_firstTime)
        {
            _firstTime = false;

            // Load persisted username on first entry
            if (!_loadedUsername && _ctx.Settings != null)
            {
                _userName = _ctx.Options.LoadUsername(_ctx.Settings);
                _loadedUsername = true;
            }
        }

        _selectedField = 0;
        // Keep username/password across re-entries so user doesn't have to retype
        _editText = _userName;

        if (_clearErrorMessageOnNextEnter)
            _errorMessage = null;
        else
            _clearErrorMessageOnNextEnter = true;
    }

    public void SetError(string error)
    {
        _errorMessage = error;
        _clearErrorMessageOnNextEnter = false;
    }

    public void HandleInput(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            CommitEdit();
            _ctx.OnReturnToMenu();
            return;
        }

        // Handle backspace
        for (int i = 0; i < input.TextInputBackspacesCount; i++)
        {
            if (_editText.Length > 0)
                _editText = _editText[..^1];
        }

        // Handle Enter — commit and advance/submit
        if (input.TextInputReturnsCount > 0)
        {
            CommitEdit();
            if (_selectedField == 0)
            {
                _selectedField = 1;
                _editText = _password;
            }
            else
            {
                TrySubmit();
            }
            return;
        }

        // Navigate between fields
        if (input.IsActionPressedOrRepeated(InputAction.MenuUp) && _selectedField > 0)
        {
            CommitEdit();
            _selectedField = 0;
            _editText = _userName;
        }
        else if (input.IsActionPressedOrRepeated(InputAction.MenuDown) && _selectedField < 1)
        {
            CommitEdit();
            _selectedField = 1;
            _editText = _password;
        }

        // Tab key = submit login
        if (input.IsActionPressed(InputAction.OpenChat))
        {
            CommitEdit();
            TrySubmit();
        }

        // Handle typed characters
        string typed = input.TextInput;
        foreach (char c in typed)
        {
            if (_selectedField == 0)
            {
                // Username: alphanumeric, space, underscore, hyphen
                if ((char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-') && _editText.Length < 16)
                    _editText += c;
            }
            else
            {
                // Password: printable chars
                if (!char.IsControl(c) && _editText.Length < 32)
                    _editText += c;
            }
        }
    }

    public void Update(float deltaTime) { }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        _menuRenderer.RenderLogin(renderer, totalCols, totalRows,
            _userName, _password, _selectedField, true, _editText, _errorMessage);
    }

    private void CommitEdit()
    {
        if (_selectedField == 0)
            _userName = _editText;
        else
            _password = _editText;
    }

    private void TrySubmit()
    {
        if (string.IsNullOrWhiteSpace(_userName))
        {
            _errorMessage = "Username cannot be empty";
            _selectedField = 0;
            _editText = _userName;
            return;
        }
        _errorMessage = null;

        // Persist username for next session
        if (_ctx.Settings != null)
            _ctx.Options.SaveUsername(_ctx.Settings, _userName);

        _ctx.OnLoginOnline(_userName, _password);
    }
}
