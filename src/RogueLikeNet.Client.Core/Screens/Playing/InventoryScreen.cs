using System.Diagnostics;
using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.Rendering.Hud;
using RogueLikeNet.Client.Core.Rendering.Overlays;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens.Playing;

/// <summary>
/// Inventory screen — item list, equipment slots, quick-slot assignment, equip/drop.
/// </summary>
public sealed class InventoryScreen : PlayingOverlayScreen
{
    private readonly InventoryRenderer _inventoryRenderer;

    /// <summary>When >= 0, the player is choosing a direction to place the buildable item at this slot.</summary>
    private int _placingSlot = -1;

    public bool IsPlacingMode => _placingSlot >= 0;

    public override ScreenState ScreenState => ScreenState.Inventory;

    public InventoryScreen(ScreenContext ctx, PlayingBackdropRenderer backdrop,
        InventoryRenderer inventoryRenderer, OverlayRenderer overlayRenderer)
        : base(ctx, backdrop, overlayRenderer)
    {
        _inventoryRenderer = inventoryRenderer;
    }

    /// <summary>When set, OnEnter will try to select this item in the inventory list.</summary>
    private int _selectItemTypeIdOnEnter;

    /// <summary>When true, OnEnter preserves the current selection/scroll state instead of resetting.</summary>
    private bool _preserveStateOnEnter;

    public void SetSelectItemOnEnter(int itemTypeId)
    {
        _selectItemTypeIdOnEnter = itemTypeId;
    }

    public void OnEnter()
    {
        _placingSlot = -1;

        if (_preserveStateOnEnter)
        {
            _preserveStateOnEnter = false;
        }
        else
        {
            _inventoryRenderer.InventoryLayout.SetFocus(1); // InvItems section
            foreach (var s in _inventoryRenderer.InventoryLayout.Sections)
            {
                s.SelectedIndex = 0;
                s.ScrollOffset = 0;
            }
        }

        // If a specific item should be selected (e.g. just crafted), find it
        if (_selectItemTypeIdOnEnter != 0)
        {
            var hud = _ctx.GameState.PlayerState;
            if (hud != null)
            {
                for (int i = 0; i < hud.InventoryItems.Length; i++)
                {
                    if (hud.InventoryItems[i].ItemTypeId == _selectItemTypeIdOnEnter)
                    {
                        _inventoryRenderer.InventoryLayout.SetFocus(1); // InvItems
                        var itemsSection = _inventoryRenderer.InventoryLayout.Sections[1];
                        itemsSection.SelectedIndex = i;
                        itemsSection.EnsureSelectionVisible(hud.InventoryCapacity);
                        break;
                    }
                }
            }
            _selectItemTypeIdOnEnter = 0;
        }
    }

    protected override void OnLeavingViaSharedNavigation(ScreenState target)
    {
        // Preserve selection + scroll state when cross-navigating between HUD panel screens.
        _preserveStateOnEnter = true;
    }

    public override void HandleInput(IInputManager input)
    {
        // Direction selection mode for placing buildable items
        if (_placingSlot >= 0)
        {
            if (input.IsActionPressed(InputAction.MenuBack))
            {
                _placingSlot = -1;
                return;
            }
            if (InputHelpers.TryReadDirection(input, out int dx, out int dy))
            {
                SendPlaceAction(_placingSlot, dx, dy);
                _placingSlot = -1;
            }
            return;
        }

        int cap = _ctx.GameState.PlayerState?.InventoryCapacity ?? 4;
        if (cap < 1) cap = 4;
        int itemCount = _ctx.GameState.PlayerState?.InventoryItems.Length ?? 0;

        if (TryHandleSharedNavigation(input)) return;

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
                HandleInvItemsInput(input, focused, itemCount);
                break;
            case "InvEquipment":
                HandleInvEquipmentInput(input, focused, itemCount);
                break;
        }
    }

    private ItemDefinition? GetSelectedItemDef()
    {
        ItemDefinition? selectedItemDef = null;

        var focused = _inventoryRenderer.InventoryLayout.FocusedSection;

        if (focused != null && focused.Name == "InvItems")
        {
            var inventory = _ctx.GameState.PlayerState?.InventoryItems;
            var selectedInventoryIndex = focused.SelectedIndex;
            if (inventory != null && selectedInventoryIndex >= 0 && selectedInventoryIndex < inventory.Length)
            {
                var itemId = inventory[selectedInventoryIndex].ItemTypeId;
                selectedItemDef = GameData.Instance.Items.Get(itemId);
            }
        }
        else if (focused != null && focused.Name == "InvEquipment")
        {
            var equipment = _ctx.GameState.PlayerState?.EquippedItems;
            var selectedEquipmentIndex = focused.SelectedIndex;

            if (equipment != null)
            {
                var slot = equipment.FirstOrDefault(e => e.EquipSlot == selectedEquipmentIndex);
                if (slot != null)
                {
                    var itemId = slot.ItemTypeId;
                    selectedItemDef = GameData.Instance.Items.Get(itemId);
                }
            }
        }

        return selectedItemDef;
    }

    protected override void RenderPanel(ISpriteRenderer renderer, int hudStartCol, int totalRows)
    {
        _inventoryRenderer.Render(renderer, _ctx.GameState, hudStartCol, totalRows, IsPlacingMode, GetSelectedItemDef());
    }

    private void HandleInvItemsInput(IInputManager input, HudSection section, int itemCount)
    {
        if (itemCount == 0)
        {
            // No items, so just cycle to next section
            _inventoryRenderer.InventoryLayout.CycleFocus();
            return;
        }

        if (section.SelectedIndex >= itemCount)
        {
            // This can happen when items are removed while the inventory screen is open. Just move selection to the last item.
            section.SelectedIndex = itemCount - 1;
            section.EnsureSelectionVisible(itemCount);
        }

        var selectedItemDef = GetSelectedItemDef();

        if (input.IsActionPressedOrRepeated(InputAction.MenuUp))
        {
            if (section.SelectedIndex > 0)
            {
                section.ScrollUp();
            }
            else
            {
                section.SelectedIndex = Math.Max(0, itemCount - 1);
                section.EnsureSelectionVisible(itemCount);
            }
        }
        else if (input.IsActionPressedOrRepeated(InputAction.MenuDown))
        {
            if (section.SelectedIndex < itemCount - 1)
            {
                section.ScrollDown(itemCount);
            }
            else
            {
                section.SelectedIndex = 0;
                section.ScrollOffset = 0;
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
        else if (input.IsActionPressed(InputAction.UseItem5))
            SendInventoryAction(ActionTypes.SetQuickSlot, 4, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.UseItem6))
            SendInventoryAction(ActionTypes.SetQuickSlot, 5, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.UseItem7))
            SendInventoryAction(ActionTypes.SetQuickSlot, 6, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.UseItem8))
            SendInventoryAction(ActionTypes.SetQuickSlot, 7, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            if (selectedItemDef != null)
            {
                if (selectedItemDef.IsPlaceable)
                    TryBeginPlace(section.SelectedIndex);
                else if (selectedItemDef.IsConsumable || selectedItemDef.IsEquippable)
                    SendInventoryAction(ActionTypes.UseItem, section.SelectedIndex);
            }
        }
        else if (input.IsActionPressed(InputAction.Drop) && section.SelectedIndex < itemCount)
            SendInventoryAction(ActionTypes.Drop, section.SelectedIndex);
        else if (input.IsActionPressed(InputAction.Place) && section.SelectedIndex < itemCount)
        {
            if (selectedItemDef != null && selectedItemDef.IsPlaceable)
                TryBeginPlace(section.SelectedIndex);
        }
    }

    private void HandleInvEquipmentInput(IInputManager input, HudSection section, int cap)
    {
        int EquipmentSlots = Equipment.SlotCount;
        if (input.IsActionPressedOrRepeated(InputAction.MenuUp))
        {
            if (section.SelectedIndex > 0)
            {
                section.ScrollUp();
            }
            else
            {
                section.SelectedIndex = EquipmentSlots - 1;
                section.EnsureSelectionVisible(EquipmentSlots);
            }
        }
        else if (input.IsActionPressedOrRepeated(InputAction.MenuDown))
        {
            if (section.SelectedIndex < EquipmentSlots - 1)
            {
                section.ScrollDown(EquipmentSlots);
            }
            else
            {
                section.SelectedIndex = 0;
                section.ScrollOffset = 0;
            }
        }
        else if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            SendInventoryAction(ActionTypes.Unequip, section.SelectedIndex);
        }
        else if (input.IsActionPressed(InputAction.Drop))
        {
            SendInventoryAction(ActionTypes.DropEquipped, section.SelectedIndex);
        }
    }

    private void TryBeginPlace(int slot)
    {
        var playerState = _ctx.GameState.PlayerState;
        if (playerState == null || slot < 0 || slot >= playerState.InventoryItems.Length) return;
        var itemDef = GameData.Instance.Items.Get(playerState.InventoryItems[slot].ItemTypeId);
        if (itemDef == null || !itemDef.IsPlaceable) return;
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

    private static int GetInvSectionItemCount(HudSection section, int itemCount) => section.Name switch
    {
        "InvItems" => itemCount,
        "InvEquipment" => Equipment.SlotCount,
        _ => 0
    };
}
