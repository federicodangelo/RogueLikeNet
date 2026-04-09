using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Save slot selection screen — list, create, load, and delete save slots for offline play.
/// </summary>
public sealed class SaveSlotScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly MenuRenderer _menuRenderer;

    private SaveSlotInfoMsg[] _slots = [];
    private int _selectedIndex;
    private int _scrollOffset;
    private int _lastTotalRows = 24;
    private string? _statusMessage;
    private bool _isError;
    private bool _confirmingDelete;
    private bool _creatingNew;
    private string _newSlotName = "";
    private bool _waitingForResponse;

    public ScreenState ScreenState => ScreenState.SaveSlotSelect;

    /// <summary>Fired when the player picks "New Game" and we need class selection.</summary>
    public Action<string, string>? OnNewGameRequested;

    /// <summary>Fired when the player picks an existing slot to load.</summary>
    public Action<string>? OnLoadSlotRequested;

    public SaveSlotScreen(ScreenContext ctx, MenuRenderer menuRenderer)
    {
        _ctx = ctx;
        _menuRenderer = menuRenderer;
    }

    public void OnEnter()
    {
        _selectedIndex = 0;
        _scrollOffset = 0;
        _statusMessage = null;
        _isError = false;
        _confirmingDelete = false;
        _creatingNew = false;
        _newSlotName = "";
        _waitingForResponse = false;
        RequestSlotList();
    }

    public void SetSlots(SaveSlotInfoMsg[] slots)
    {
        _slots = slots.OrderByDescending(s => s.LastSavedAtUnixMs).ToArray();
        _waitingForResponse = false;
        if (_selectedIndex >= TotalItemCount)
            _selectedIndex = Math.Max(0, TotalItemCount - 1);
        _scrollOffset = Math.Min(_scrollOffset, Math.Max(0, _selectedIndex));
        EnsureSelectionVisible();
    }

    public void SetStatus(string message, bool isError)
    {
        _statusMessage = message;
        _isError = isError;
        _waitingForResponse = false;
    }

    /// <summary>Total menu items: slots + "New Game" action.</summary>
    private int TotalItemCount => _slots.Length + 1;

    /// <summary>Whether the currently selected index is the "New Game" action at the bottom.</summary>
    private bool IsNewGameSelected => _selectedIndex == 0;

    private SaveSlotInfoMsg? SelectedSlot => IsNewGameSelected ? null : _slots[_selectedIndex - 1];

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
            // Full cleanup: tears down the embedded server/connection created for slot browsing
            _ctx.OnReturnToMenu();
            return;
        }

        int count = TotalItemCount;
        if (input.IsActionPressed(InputAction.MenuUp))
        {
            _selectedIndex = (_selectedIndex + count - 1) % count;
            EnsureSelectionVisible();
        }
        else if (input.IsActionPressed(InputAction.MenuDown))
        {
            _selectedIndex = (_selectedIndex + 1) % count;
            EnsureSelectionVisible();
        }

        if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            if (IsNewGameSelected)
            {
                _creatingNew = true;
                _newSlotName = "";
                _statusMessage = null;
            }
            else
            {
                // Load the selected slot
                var slot = SelectedSlot;
                if (slot == null) return;
                _waitingForResponse = true;
                OnLoadSlotRequested?.Invoke(slot.SlotId);
            }
        }

        // Delete with X / Delete key on a slot
        if (input.IsActionPressed(InputAction.MenuSecondaryAction) && !IsNewGameSelected && _slots.Length > 0)
        {
            _confirmingDelete = true;
            _statusMessage = null;
        }
    }

    public void Update(float deltaTime) { }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        _lastTotalRows = totalRows;
        SaveSlotMenuRenderer.RenderSaveSlotScreen(renderer, totalCols, totalRows,
            _slots, SelectedSlot, _scrollOffset, _statusMessage, _isError,
            _confirmingDelete, _creatingNew, _newSlotName, _waitingForResponse);
    }

    private void HandleDeleteConfirmation(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            var slot = SelectedSlot;
            if (slot == null) return;
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
                OnNewGameRequested?.Invoke(_newSlotName, "");
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

    /// <summary>
    /// Adjusts <see cref="_scrollOffset"/> so that <see cref="_selectedIndex"/> falls
    /// within the visible item window.
    /// </summary>
    private void EnsureSelectionVisible()
    {
        _scrollOffset = SaveSlotMenuRenderer.EnsureSaveSlotContentVisible(_lastTotalRows, _slots.Length, _scrollOffset, _selectedIndex);
    }
}
