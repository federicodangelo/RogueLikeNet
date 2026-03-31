using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Server admin screen — manage save slots on a connected server (list, create, load, delete, save current).
/// Accessible from the pause menu when connected online.
/// </summary>
public sealed class ServerAdminScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly PlayingScreen _playingScreen;
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

    public ServerAdminScreen(ScreenContext ctx, PlayingScreen playingScreen, MenuRenderer menuRenderer)
    {
        _ctx = ctx;
        _playingScreen = playingScreen;
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
    /// Total menu items: slots + New Game + Save Current + Back.
    /// </summary>
    private int TotalItemCount => _slots.Length + 3;

    private int NewGameIndex => _slots.Length;
    private int SaveCurrentIndex => _slots.Length + 1;
    private int BackIndex => _slots.Length + 2;

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
            _ctx.RequestTransition(Rendering.ScreenState.Paused);
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
                _ctx.RequestTransition(Rendering.ScreenState.Paused);
            }
            else if (_selectedIndex == NewGameIndex)
            {
                _creatingNew = true;
                _newSlotName = "";
            }
            else if (_selectedIndex == SaveCurrentIndex)
            {
                _waitingForResponse = true;
                _ = _ctx.Connection?.SendSaveGameCommandAsync(new SaveGameCommandMsg
                {
                    Action = SaveGameAction.Save,
                });
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

    public void Update(float deltaTime) { }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        _playingScreen.Render(renderer, totalCols, totalRows);
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
