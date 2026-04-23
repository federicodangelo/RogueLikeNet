using System.Diagnostics;
using System.Text;
using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.Rendering.Hud;
using RogueLikeNet.Client.Core.Rendering.Overlays;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens.Playing;

/// <summary>
/// Main gameplay screen — handles movement, combat, skills, quick slots, and renders the game world with HUD.
/// </summary>
public sealed class PlayingScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly PlayingBackdropRenderer _backdrop;
    private readonly HudRenderer _hudRenderer;
    private readonly OverlayRenderer _overlayRenderer;

    // Movement hold-to-repeat
    private static readonly long MoveRepeatDelayTicks = Stopwatch.Frequency / 4; // 250 ms
    private InputAction? _heldMoveAction;
    private long _moveHeldSinceTicks;

    // Pick-up-placed direction selection mode
    private bool _pickingUpPlaced;
    public bool IsPickingUpPlaced => _pickingUpPlaced;

    // Look around
    private bool _lookingAround;
    public bool IsLookingAround => _lookingAround;

    // Quick-slot placement direction selection mode (-1 = inactive, >= 0 = inventory slot to place)
    private int _placingFromSlot = -1;
    public bool IsPlacingFromSlot => _placingFromSlot >= 0;

    // Farming interact direction selection mode
    private bool _interacting;
    public bool IsInteracting => _interacting;

    public ScreenState ScreenState => ScreenState.Playing;

    public PlayingScreen(ScreenContext ctx, PlayingBackdropRenderer backdrop, HudRenderer hudRenderer,
        OverlayRenderer overlayRenderer)
    {
        _ctx = ctx;
        _backdrop = backdrop;
        _hudRenderer = hudRenderer;
        _overlayRenderer = overlayRenderer;
    }

    public void HandleInput(IInputManager input)
    {
        if (_ctx.Chat.InputActive)
        {
            _ctx.Chat.HandleInput(input, _ctx.Connection);
            return;
        }
        else if (_pickingUpPlaced) // Direction selection mode for picking up placed buildable tiles
        {
            HandlePickingUpPlacedInput(input);
            return;
        }
        else if (_placingFromSlot >= 0)// Direction selection mode for placing a buildable item from quick slot
        {
            HandlePlacingFromSlotInput(input);
            return;
        }
        else if (_interacting)// Direction selection mode for farming interact (context-sensitive)
        {
            HandleInteractingInput(input);
            return;
        }
        else if (_lookingAround)
        {
            HandleLookingAroundInput(input);
            return;
        }

        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _ctx.RequestTransition(Rendering.ScreenState.Paused);
            return;
        }

        if (input.IsActionPressed(InputAction.OpenInventory))
        {
            _ctx.RequestTransition(Rendering.ScreenState.Inventory);
            return;
        }

        if (input.IsActionPressed(InputAction.OpenCrafting))
        {
            _ctx.RequestTransition(Rendering.ScreenState.Crafting);
            return;
        }

        if (input.IsActionPressed(InputAction.OpenQuestLog))
        {
            _ctx.RequestTransition(Rendering.ScreenState.QuestLog);
            return;
        }

        if (input.IsActionPressed(InputAction.OpenChat))
        {
            _ctx.Chat.InputActive = true;
            _ctx.Chat.InputText = "";
            return;
        }

        // Debug key toggles (only in debug mode)
        _ctx.Debug.HandleDebugKeys(input, _ctx.DebugSyncRequested);

        ClientInputMsg? msg = null;

        // Determine held movement direction, applying a repeat delay
        InputAction? activeMoveAction = null;
        if (input.IsActionDown(InputAction.MoveUp)) activeMoveAction = InputAction.MoveUp;
        else if (input.IsActionDown(InputAction.MoveDown)) activeMoveAction = InputAction.MoveDown;
        else if (input.IsActionDown(InputAction.MoveLeft)) activeMoveAction = InputAction.MoveLeft;
        else if (input.IsActionDown(InputAction.MoveRight)) activeMoveAction = InputAction.MoveRight;

        if (activeMoveAction != null)
        {
            long now = Stopwatch.GetTimestamp();
            if (_heldMoveAction != activeMoveAction)
            {
                _heldMoveAction = activeMoveAction;
                _moveHeldSinceTicks = now;
            }

            bool fireMove = input.IsActionPressed(activeMoveAction.Value)
                            || now - _moveHeldSinceTicks >= MoveRepeatDelayTicks;
            if (fireMove)
            {
                int dx = 0, dy = 0;
                switch (activeMoveAction.Value)
                {
                    case InputAction.MoveUp: dy = -1; break;
                    case InputAction.MoveDown: dy = 1; break;
                    case InputAction.MoveLeft: dx = -1; break;
                    case InputAction.MoveRight: dx = 1; break;
                }

                // In max speed mode, move 4 tiles at a time by sending 4 commands
                msg = new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = dx, TargetY = dy };
                SendInput(msg);
                msg = null; // already sent
            }
        }
        else
        {
            _heldMoveAction = null;
        }

        if (msg == null)
        {
            if (input.IsActionPressed(InputAction.Wait))
                msg = new ClientInputMsg { ActionType = ActionTypes.Wait };
            else if (input.IsActionPressed(InputAction.Attack))
                msg = new ClientInputMsg { ActionType = ActionTypes.Attack, TargetX = 0, TargetY = 0 };
            else if (input.IsActionPressed(InputAction.PickUp))
                msg = new ClientInputMsg { ActionType = ActionTypes.PickUp };
            else if (input.IsActionPressed(InputAction.UseItem1))
                msg = TryQuickSlotAction(0);
            else if (input.IsActionPressed(InputAction.UseItem2))
                msg = TryQuickSlotAction(1);
            else if (input.IsActionPressed(InputAction.UseItem3))
                msg = TryQuickSlotAction(2);
            else if (input.IsActionPressed(InputAction.UseItem4))
                msg = TryQuickSlotAction(3);
            else if (input.IsActionPressed(InputAction.UseItem5))
                msg = TryQuickSlotAction(4);
            else if (input.IsActionPressed(InputAction.UseItem6))
                msg = TryQuickSlotAction(5);
            else if (input.IsActionPressed(InputAction.UseItem7))
                msg = TryQuickSlotAction(6);
            else if (input.IsActionPressed(InputAction.UseItem8))
                msg = TryQuickSlotAction(7);
            else if (input.IsActionPressed(InputAction.Place))
                _pickingUpPlaced = true;
            else if (input.IsActionPressed(InputAction.Interact))
                _interacting = true;
            else if (input.IsActionPressed(InputAction.Look))
                _lookingAround = true;
            else if (input.IsActionPressed(InputAction.UseStairs))
                msg = new ClientInputMsg { ActionType = ActionTypes.UseStairs };
            else if (input.IsActionPressed(InputAction.CastSpell))
                msg = TryCastSpell();
        }

        if (msg != null)
            SendInput(msg);
    }

    private void HandleLookingAroundInput(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _lookingAround = false;
            return;
        }
        if (!InputHelpers.TryReadDirection(input, out int dx, out int dy, includeMenuArrows: false))
            return;
        {
            // Find item at the given position and show its info in the HUD
            int lookX = _ctx.GameState.PlayerX + dx;
            int lookY = _ctx.GameState.PlayerY + dy;

            var tile = _ctx.GameState.GetTile(lookX, lookY);
            var entity = _ctx.GameState.Entities.Values.FirstOrDefault(e => e.X == lookX && e.Y == lookY && e.Z == _ctx.GameState.PlayerZ);

            string? description = null;

            if (tile.PlaceableItemId != 0)
            {
                description = AsciiDraw.ItemDisplayName(tile.PlaceableItemId);
            }
            else if (entity != null)
            {
                if (entity.Item != null)
                {
                    description = $"{AsciiDraw.ItemDisplayName(entity.Item.ItemTypeId)}{(entity.Item.StackCount > 1 ? $" x{entity.Item.StackCount}" : "")}";
                }
                else
                {
                    switch (entity.EntityType)
                    {
                        case EntityType.ResourceNode:
                            description = "Resource Node";
                            break;
                        case EntityType.TownNpc:
                            description = "Town NPC";
                            break;
                        case EntityType.Monster:
                            description = "Enemy";
                            break;
                        default:
                            description = $"{entity.EntityType}";
                            break;
                    }
                }
            }
            else
            {
                if (tile.TileId != 0)
                    description = GameData.Instance.Tiles.Get(tile.TileId)?.Name;
            }

            var direction = dx == 0 ? (dy == -1 ? "North" : "South") : (dx == -1 ? "West" : "East");
            _ctx.Chat.AddChatLine($"{direction}: {description ?? "Empty"}");

            _lookingAround = false;
        }
    }

    private void HandleInteractingInput(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _interacting = false;
            return;
        }
        if (InputHelpers.TryReadDirection(input, out int dx, out int dy, includeMenuArrows: false))
        {
            SendInput(new ClientInputMsg
            {
                ActionType = ActionTypes.Interact,
                TargetX = dx,
                TargetY = dy,
                Tick = _ctx.GameState.WorldTick
            });
            _interacting = false;
        }
    }

    private void HandlePlacingFromSlotInput(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _placingFromSlot = -1;
            return;
        }
        if (InputHelpers.TryReadDirection(input, out int dx, out int dy, includeMenuArrows: false))
        {
            SendInput(new ClientInputMsg
            {
                ActionType = ActionTypes.PlaceItem,
                ItemSlot = _placingFromSlot,
                TargetX = dx,
                TargetY = dy,
                Tick = _ctx.GameState.WorldTick
            });
            _placingFromSlot = -1;
        }
    }

    private void HandlePickingUpPlacedInput(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _pickingUpPlaced = false;
            return;
        }
        if (InputHelpers.TryReadDirection(input, out int dx, out int dy, includeMenuArrows: false))
        {
            SendInput(new ClientInputMsg
            {
                ActionType = ActionTypes.PickUpPlaced,
                TargetX = dx,
                TargetY = dy,
                Tick = _ctx.GameState.WorldTick
            });
            _pickingUpPlaced = false;
        }
    }

    /// <summary>
    /// Resolves a quick-slot press: if the item is buildable, enters placement direction mode;
    /// otherwise returns a UseQuickSlot message.
    /// </summary>
    private ClientInputMsg? TryQuickSlotAction(int slotNum)
    {
        var ps = _ctx.GameState.PlayerState;
        if (ps != null && slotNum < ps.QuickSlotIndices.Length)
        {
            int invIndex = ps.QuickSlotIndices[slotNum];
            if (invIndex >= 0 && invIndex < ps.InventoryItems.Length &&
             (GameData.Instance.Items.Get(ps.InventoryItems[invIndex].ItemTypeId)?.IsPlaceable ?? false))
            {
                _placingFromSlot = invIndex;
                return null;
            }
        }
        return new ClientInputMsg { ActionType = ActionTypes.UseQuickSlot, ItemSlot = slotNum };
    }

    private void SendInput(ClientInputMsg msg)
    {
        if (_ctx.Connection != null)
        {
            msg.Tick = _ctx.GameState.WorldTick;
            _ = _ctx.Connection.SendInputAsync(msg);
        }
    }

    private ClientInputMsg? TryCastSpell()
    {
        var ps = _ctx.GameState.PlayerState;
        if (ps == null) return null;

        // Find spells from equipped weapon
        var hand = ps.EquippedItems.FirstOrDefault(eq => eq.EquipSlot == (int)EquipSlot.Hand);
        if (hand == null) return null;

        var def = GameData.Instance.Items.Get(hand.ItemTypeId);
        if (def?.Magic == null || string.IsNullOrEmpty(def.Magic.SpellId)) return null;

        var spell = GameData.Instance.Spells.Get(def.Magic.SpellId);
        if (spell == null) return null;

        return new ClientInputMsg { ActionType = ActionTypes.CastSpell, ItemSlot = spell.NumericId };
    }

    public void Update(float deltaTime)
    {
        _ctx.Particles.Update(deltaTime);
        _ctx.ScreenShake.Update(_ctx.GameState.PlayerState?.Health ?? 0);
    }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        int gameCols = _backdrop.RenderWorld(renderer, _ctx, totalCols, totalRows);

        _hudRenderer.Render(renderer, _ctx.GameState, gameCols, totalRows,
            directionalInteractionMode:
                _pickingUpPlaced ? HudRenderer.DirectionalInteractionMode.PickUp :
                _placingFromSlot >= 0 ? HudRenderer.DirectionalInteractionMode.Place :
                _interacting ? HudRenderer.DirectionalInteractionMode.Interact :
                _lookingAround ? HudRenderer.DirectionalInteractionMode.Look :
                HudRenderer.DirectionalInteractionMode.None
        );

        _backdrop.RenderParticles(renderer, _ctx, totalCols, totalRows);

        _overlayRenderer.RenderChat(renderer, totalCols, totalRows, _ctx.Chat);
        if (_ctx.Options.ShowStats)
            _overlayRenderer.RenderPerformance(renderer, gameCols, _ctx.Performance, _ctx.Debug);
        if (_ctx.Options.ShowQuestTracker)
            _overlayRenderer.RenderQuestTracker(renderer, gameCols, totalRows, _ctx.GameState.PlayerState);
    }
}
