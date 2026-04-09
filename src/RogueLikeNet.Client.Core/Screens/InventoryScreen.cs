using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Inventory screen — item list, equipment slots, quick-slot assignment, equip/drop.
/// </summary>
public sealed class InventoryScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly GameWorldRenderer _worldRenderer;
    private readonly InventoryRenderer _inventoryRenderer;
    private readonly OverlayRenderer _overlayRenderer;

    /// <summary>When >= 0, the player is choosing a direction to place the buildable item at this slot.</summary>
    private int _placingSlot = -1;

    public bool IsPlacingMode => _placingSlot >= 0;

    public ScreenState ScreenState => ScreenState.Inventory;

    public InventoryScreen(ScreenContext ctx, GameWorldRenderer worldRenderer,
        InventoryRenderer inventoryRenderer, OverlayRenderer overlayRenderer)
    {
        _ctx = ctx;
        _worldRenderer = worldRenderer;
        _inventoryRenderer = inventoryRenderer;
        _overlayRenderer = overlayRenderer;
    }

    public void OnEnter()
    {
        _placingSlot = -1;
        _inventoryRenderer.InventoryLayout.SetFocus(1); // InvItems section
        foreach (var s in _inventoryRenderer.InventoryLayout.Sections)
        {
            s.SelectedIndex = 0;
            s.ScrollOffset = 0;
        }
    }

    public void HandleInput(IInputManager input)
    {
        // Direction selection mode for placing buildable items
        if (_placingSlot >= 0)
        {
            if (input.IsActionPressed(InputAction.MenuBack))
            {
                _placingSlot = -1;
                return;
            }
            int dx = 0, dy = 0;
            if (input.IsActionPressed(InputAction.MoveUp) || input.IsActionPressed(InputAction.MenuUp)) { dy = -1; }
            else if (input.IsActionPressed(InputAction.MoveDown) || input.IsActionPressed(InputAction.MenuDown)) { dy = 1; }
            else if (input.IsActionPressed(InputAction.MoveLeft)) { dx = -1; }
            else if (input.IsActionPressed(InputAction.MoveRight)) { dx = 1; }
            if (dx != 0 || dy != 0)
            {
                SendPlaceAction(_placingSlot, dx, dy);
                _placingSlot = -1;
            }
            return;
        }

        int cap = _ctx.GameState.PlayerState?.InventoryCapacity ?? 4;
        if (cap < 1) cap = 4;

        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _ctx.RequestTransition(Rendering.ScreenState.Playing);
            return;
        }

        if (input.IsActionPressed(InputAction.CycleSection))
        {
            _inventoryRenderer.InventoryLayout.CycleFocus();
            return;
        }

        var focused = _inventoryRenderer.InventoryLayout.FocusedSection;
        if (focused == null) return;

        switch (focused.Name)
        {
            case "InvItems":
                HandleInvItemsInput(input, focused, cap);
                break;
            case "InvEquipment":
                HandleInvEquipmentInput(input, focused, cap);
                break;
        }
    }

    public void Update(float deltaTime)
    {
        _ctx.Particles.Update(deltaTime);
        _ctx.ScreenShake.Update(_ctx.GameState.PlayerState?.Health ?? 0);
    }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        int gameCols = totalCols - AsciiDraw.HudColumns;
        float shakeX = _ctx.ScreenShake.OffsetX;
        float shakeY = _ctx.ScreenShake.OffsetY;

        // Zoom only affects the game world area, not HUD or UI
        var debug = _ctx.Debug;
        int tileW = debug.EffectiveTileWidth;
        int tileH = debug.EffectiveTileHeight;
        float fontScale = debug.EffectiveFontScale;

        // Compute how many zoomed tiles fit in the game area pixel space
        int gamePixelW = gameCols * AsciiDraw.TileWidth;
        int gamePixelH = totalRows * AsciiDraw.TileHeight;
        int zoomedGameCols = Math.Max(1, gamePixelW / tileW);
        int zoomedRows = Math.Max(1, gamePixelH / tileH);

        renderer.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);
        bool debugLightOff = debug is { Enabled: true, LightOff: true };
        _worldRenderer.Render(renderer, _ctx.GameState, zoomedGameCols, zoomedRows, shakeX, shakeY, tileW, tileH, fontScale, debugLightOff);
        _inventoryRenderer.Render(renderer, _ctx.GameState, gameCols, totalRows, IsPlacingMode);

        // Render particles
        int halfW = zoomedGameCols / 2;
        int halfH = zoomedRows / 2;
        _ctx.Particles.Render(renderer, _ctx.GameState.PlayerX, _ctx.GameState.PlayerY,
            halfW, halfH, shakeX, shakeY, tileW, tileH);

        _overlayRenderer.RenderChat(renderer, totalCols, totalRows, _ctx.Chat);
        _overlayRenderer.RenderPerformance(renderer, _ctx.Performance, _ctx.Debug);
    }

    private void HandleInvItemsInput(IInputManager input, HudSection section, int cap)
    {
        if (input.IsActionPressed(InputAction.MenuUp))
        {
            if (section.SelectedIndex > 0)
                section.ScrollUp();
            else
            {
                var prev = _inventoryRenderer.InventoryLayout.FocusPreviousInputSection();
                if (prev != null && prev != section)
                {
                    int prevMax = GetInvSectionItemCount(prev, cap);
                    prev.SelectedIndex = Math.Max(0, prevMax - 1);
                    prev.EnsureSelectionVisible();
                }
                else
                {
                    section.SelectedIndex = Math.Max(0, cap - 1);
                    section.EnsureSelectionVisible();
                }
            }
        }
        else if (input.IsActionPressed(InputAction.MenuDown))
        {
            if (section.SelectedIndex < cap - 1)
                section.ScrollDown(cap);
            else
            {
                var next = _inventoryRenderer.InventoryLayout.FocusNextInputSection();
                if (next != null && next != section)
                {
                    next.SelectedIndex = 0;
                    next.ScrollOffset = 0;
                }
                else
                {
                    section.SelectedIndex = 0;
                    section.ScrollOffset = 0;
                }
            }
        }
        else if (input.IsActionPressed(InputAction.UseItem1))
            SendInventoryAction(ActionTypes.SetQuickSlot, 0, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.UseItem2))
            SendInventoryAction(ActionTypes.SetQuickSlot, 1, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.UseItem3))
            SendInventoryAction(ActionTypes.SetQuickSlot, 2, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.UseItem4))
            SendInventoryAction(ActionTypes.SetQuickSlot, 3, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.MenuConfirm))
            SendInventoryAction(ActionTypes.UseItem, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.Equip) && section.SelectedIndex < cap)
            SendInventoryAction(ActionTypes.Equip, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.Drop) && section.SelectedIndex < cap)
            SendInventoryAction(ActionTypes.Drop, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.Place) && section.SelectedIndex < cap)
            TryBeginPlace(section.SelectedIndex);
    }

    private void HandleInvEquipmentInput(IInputManager input, HudSection section, int cap)
    {
        int EquipmentSlots = Equipment.SlotCount;
        if (input.IsActionPressed(InputAction.MenuUp))
        {
            if (section.SelectedIndex > 0)
                section.SelectedIndex--;
            else
            {
                var prev = _inventoryRenderer.InventoryLayout.FocusPreviousInputSection();
                if (prev != null && prev != section)
                {
                    int prevMax = GetInvSectionItemCount(prev, cap);
                    prev.SelectedIndex = Math.Max(0, prevMax - 1);
                    prev.EnsureSelectionVisible();
                }
                else
                {
                    section.SelectedIndex = EquipmentSlots - 1;
                }
            }
        }
        else if (input.IsActionPressed(InputAction.MenuDown))
        {
            if (section.SelectedIndex < EquipmentSlots - 1)
                section.SelectedIndex++;
            else
            {
                var next = _inventoryRenderer.InventoryLayout.FocusNextInputSection();
                if (next != null && next != section)
                {
                    next.SelectedIndex = 0;
                    next.ScrollOffset = 0;
                }
                else
                {
                    section.SelectedIndex = 0;
                }
            }
        }
        else if (input.IsActionPressed(InputAction.MenuConfirm) || input.IsActionPressed(InputAction.Equip))
        {
            SendInventoryAction(ActionTypes.Unequip, section.SelectedIndex);
        }
    }

    private void TryBeginPlace(int slot)
    {
        var hud = _ctx.GameState.PlayerState;
        if (hud == null || slot < 0 || slot >= hud.InventoryItems.Length) return;
        if (hud.InventoryItems[slot].Category != ItemDefinitions.CategoryPlaceable) return;
        _placingSlot = slot;
    }

    private void SendPlaceAction(int slot, int dx, int dy)
    {
        if (_ctx.Connection == null) return;
        var msg = new ClientInputMsg
        {
            ActionType = ActionTypes.PlaceItem,
            ItemSlot = slot,
            TargetX = dx,
            TargetY = dy,
            Tick = _ctx.GameState.WorldTick
        };
        _ = _ctx.Connection.SendInputAsync(msg);
    }

    private void SendInventoryAction(int actionType, int slot, int targetSlot = 0)
    {
        if (_ctx.Connection == null) return;
        var msg = new ClientInputMsg
        {
            ActionType = actionType,
            ItemSlot = slot,
            TargetSlot = targetSlot,
            Tick = _ctx.GameState.WorldTick
        };
        _ = _ctx.Connection.SendInputAsync(msg);
    }

    private static int GetInvSectionItemCount(HudSection section, int cap) => section.Name switch
    {
        "InvItems" => cap,
        "InvEquipment" => Equipment.SlotCount,
        _ => 0
    };
}
