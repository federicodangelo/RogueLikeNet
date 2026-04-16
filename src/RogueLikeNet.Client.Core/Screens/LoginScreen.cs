using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Login screen — enter username and password before connecting to an online server.
/// </summary>
public sealed class LoginScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly MenuRenderer _menuRenderer;

    private string _userName = "";
    private string _password = "";
    private int _selectedField; // 0 = username, 1 = password
    private bool _isEditing;
    private string _editText = "";
    private string? _errorMessage;

    public ScreenState ScreenState => ScreenState.Login;

    /// <summary>The player name entered by the user (available after login).</summary>
    public string UserName => _userName;

    /// <summary>The password entered by the user (available after login).</summary>
    public string Password => _password;

    public LoginScreen(ScreenContext ctx, MenuRenderer menuRenderer)
    {
        _ctx = ctx;
        _menuRenderer = menuRenderer;
    }

    public void OnEnter()
    {
        _selectedField = 0;
        _isEditing = false;
        _editText = "";
        _errorMessage = null;
        // Keep username/password across re-entries so user doesn't have to retype
    }

    public void SetError(string error)
    {
        _errorMessage = error;
    }

    public void HandleInput(IInputManager input)
    {
        if (_isEditing)
        {
            HandleEditing(input);
            return;
        }

        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _ctx.OnReturnToMenu();
            return;
        }

        if (input.IsActionPressedOrRepeated(InputAction.MenuUp))
            _selectedField = 0;
        else if (input.IsActionPressedOrRepeated(InputAction.MenuDown))
            _selectedField = 1;

        if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            if (_selectedField <= 1)
            {
                // Start editing the selected field
                _isEditing = true;
                _editText = _selectedField == 0 ? _userName : _password;
                _errorMessage = null;
            }
        }

        // Tab key = submit login
        if (input.IsActionPressed(InputAction.OpenChat))
        {
            TrySubmit();
        }
    }

    public void Update(float deltaTime) { }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        _menuRenderer.RenderLogin(renderer, totalCols, totalRows,
            _userName, _password, _selectedField, _isEditing, _editText, _errorMessage);
    }

    private void HandleEditing(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _isEditing = false;
            return;
        }

        for (int i = 0; i < input.TextInputBackspacesCount; i++)
        {
            if (_editText.Length > 0)
                _editText = _editText[..^1];
        }

        if (input.TextInputReturnsCount > 0)
        {
            // Commit the edit
            if (_selectedField == 0)
                _userName = _editText;
            else
                _password = _editText;
            _isEditing = false;

            // Auto-advance to next field or submit
            if (_selectedField == 0)
                _selectedField = 1;
            else
                TrySubmit();
            return;
        }

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

    private void TrySubmit()
    {
        if (string.IsNullOrWhiteSpace(_userName))
        {
            _errorMessage = "Username cannot be empty";
            _selectedField = 0;
            return;
        }
        _errorMessage = null;
        _ctx.OnLoginOnline(_userName, _password);
    }
}
