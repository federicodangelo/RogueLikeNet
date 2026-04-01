using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Server admin screen — manage save slots on a connected server (list, create, load, delete, save current).
/// Accessible from the main menu via "Admin Online".
/// </summary>
public sealed class ServerAdminScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly MenuRenderer _menuRenderer;

    private SaveSlotInfoMsg[] _slots = [];
    private string _currentSlotId = "";
    private int _selectedIndex;
    private string? _statusMessage;
    private bool _isError;
    private bool _confirmingDelete;
    private bool _creatingNew;
    private string _newSlotName = "";
    private bool _waitingForResponse;

    public ScreenState ScreenState => ScreenState.ServerAdmin;

    public ServerAdminScreen(ScreenContext ctx, MenuRenderer menuRenderer)
    {
        _ctx = ctx;
        _menuRenderer = menuRenderer;
    }

    public void OnEnter()
    {
        _selectedIndex = 0;
        _statusMessage = null;
        _isError = false;
        _confirmingDelete = false;
        _creatingNew = false;
        _newSlotName = "";
        _waitingForResponse = false;
        RequestSlotList();
    }

    public void HandleSaveResponse(SaveGameResponseMsg response)
    {
        _waitingForResponse = false;

        _slots = response.Slots;
        _currentSlotId = response.CurrentSlotId;
        if (_selectedIndex >= TotalItemCount)
            _selectedIndex = Math.Max(0, TotalItemCount - 1);

        if (!string.IsNullOrEmpty(response.Message))
        {
            _statusMessage = response.Message;
            _isError = !response.Success;
        }
        else if (response.Success)
        {
            _statusMessage = response.Action switch
            {
                SaveGameAction.New => "New game created",
                SaveGameAction.Load => "Game loaded",
                SaveGameAction.Delete => "Slot deleted",
                SaveGameAction.Save => "Game saved",
                _ => null,
            };
            _isError = false;
        }
    }

    /// <summary>
    /// Total menu items: slots + New Game + Back.
    /// </summary>
    private int TotalItemCount => _slots.Length + 2;

    private int NewGameIndex => _slots.Length;
    private int BackIndex => _slots.Length + 1;

    public void HandleInput(IInputManager input)
    {
        if (_waitingForResponse) return;

        if (_confirmingDelete)
        {
            HandleDeleteConfirmation(input);
            return;
        }

        if (_creatingNew)
        {
            HandleNewSlotNaming(input);
            return;
        }

        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _ctx.OnReturnToMenu();
            return;
        }

        int count = TotalItemCount;
        if (input.IsActionPressed(InputAction.MenuUp))
            _selectedIndex = (_selectedIndex + count - 1) % count;
        else if (input.IsActionPressed(InputAction.MenuDown))
            _selectedIndex = (_selectedIndex + 1) % count;

        if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            _statusMessage = null;

            if (_selectedIndex == BackIndex)
            {
                _ctx.OnReturnToMenu();
            }
            else if (_selectedIndex == NewGameIndex)
            {
                _creatingNew = true;
                _newSlotName = "";
            }
            else
            {
                // Load the selected slot
                var slot = _slots[_selectedIndex];
                _waitingForResponse = true;
                _ = _ctx.Connection?.SendSaveGameCommandAsync(new SaveGameCommandMsg
                {
                    Action = SaveGameAction.Load,
                    SlotId = slot.SlotId,
                });
            }
        }

        // Delete with X / Delete key on a slot
        if (input.IsActionPressed(InputAction.MenuSecondaryAction) && _selectedIndex < _slots.Length && _slots.Length > 0)
        {
            _confirmingDelete = true;
            _statusMessage = null;
        }
    }

    public void Update(float deltaTime)
    {
        if (_ctx.Connection == null || !_ctx.Connection.IsConnected)
        {
            _ctx.OnReturnToMenu();
        }
    }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        renderer.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);
        _menuRenderer.RenderServerAdmin(renderer, totalCols, totalRows,
            _slots, _currentSlotId, _selectedIndex, _statusMessage, _isError,
            _confirmingDelete, _creatingNew, _newSlotName, _waitingForResponse);
    }

    private void HandleDeleteConfirmation(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            var slot = _slots[_selectedIndex];
            _confirmingDelete = false;
            _waitingForResponse = true;
            _statusMessage = null;
            _ = _ctx.Connection?.SendSaveGameCommandAsync(new SaveGameCommandMsg
            {
                Action = SaveGameAction.Delete,
                SlotId = slot.SlotId,
            });
        }
        else if (input.IsActionPressed(InputAction.MenuBack))
        {
            _confirmingDelete = false;
        }
    }

    private void HandleNewSlotNaming(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _creatingNew = false;
            return;
        }

        for (int i = 0; i < input.TextInputBackspacesCount; i++)
        {
            if (_newSlotName.Length > 0)
                _newSlotName = _newSlotName[..^1];
        }

        if (input.TextInputReturnsCount > 0)
        {
            if (_newSlotName.Length > 0)
            {
                _creatingNew = false;
                _waitingForResponse = true;
                _ = _ctx.Connection?.SendSaveGameCommandAsync(new SaveGameCommandMsg
                {
                    Action = SaveGameAction.New,
                    SlotName = _newSlotName,
                });
            }
            return;
        }

        string typed = input.TextInput;
        foreach (char c in typed)
        {
            if ((char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '\'') && _newSlotName.Length < 24)
                _newSlotName += c;
        }
    }

    private void RequestSlotList()
    {
        _waitingForResponse = true;
        _ = _ctx.Connection?.SendSaveGameCommandAsync(new SaveGameCommandMsg
        {
            Action = SaveGameAction.List,
        });
    }
}
