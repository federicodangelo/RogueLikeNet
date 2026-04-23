using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.Rendering.Hud;
using RogueLikeNet.Client.Core.Rendering.Overlays;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens.Playing;

/// <summary>
/// Modal shown when bumping into a town NPC that has quest offers, quest
/// turn-ins, or a shop. Lets the player pick an action and dispatches the
/// corresponding <see cref="ClientInputMsg"/>. Rendering is delegated to
/// <see cref="NpcDialogueRenderer"/>.
/// </summary>
public sealed class NpcDialogueScreen : PlayingOverlayScreen
{
    private readonly NpcDialogueRenderer _dialogueRenderer;

    private NpcInteractionMsg? _interaction;
    private int _selectedIndex;

    private readonly List<NpcDialogueAction> _actions = new();

    // Shop sub-view state — active when the player picks "Browse shop".
    private enum SubMode { ActionList, Shop }
    private SubMode _subMode;
    private ShopDefinition? _shopDef;
    private TownNpcRole _shopRole;
    private bool _shopSellMode;
    private int _shopSelected;
    private int _shopScroll;

    public override ScreenState ScreenState => ScreenState.NpcDialogue;

    public NpcDialogueScreen(ScreenContext ctx, PlayingBackdropRenderer backdrop,
        NpcDialogueRenderer dialogueRenderer, OverlayRenderer overlayRenderer)
        : base(ctx, backdrop, overlayRenderer)
    {
        _dialogueRenderer = dialogueRenderer;
    }

    /// <summary>Open the dialogue modal for the given interaction.</summary>
    public void OpenFor(NpcInteractionMsg interaction)
    {
        _interaction = interaction;
        _selectedIndex = 0;
        _subMode = SubMode.ActionList;
        _shopDef = null;
        _shopSellMode = false;
        _shopSelected = 0;
        _shopScroll = 0;
        RebuildActions();
    }

    public void OnEnter()
    {
        RebuildActions();
    }

    private void RebuildActions()
    {
        _actions.Clear();
        if (_interaction == null) return;

        foreach (var turnIn in _interaction.QuestTurnIns)
        {
            string label = turnIn.IsComplete
                ? $"Turn in: {turnIn.Title}"
                : $"Report progress: {turnIn.Title}";
            _actions.Add(new NpcDialogueAction(
                NpcDialogueActionKind.TurnIn, turnIn.QuestNumericId, label, turnIn.IsComplete));
        }

        foreach (var offer in _interaction.QuestOffers)
        {
            _actions.Add(new NpcDialogueAction(
                NpcDialogueActionKind.AcceptOffer, offer.QuestNumericId, $"Accept: {offer.Title}", enabled: true));
        }

        if (_interaction.HasShop)
        {
            _actions.Add(new NpcDialogueAction(
                NpcDialogueActionKind.OpenShop, 0, "Browse shop", enabled: true));
        }

        _actions.Add(new NpcDialogueAction(NpcDialogueActionKind.Leave, 0, "Leave", enabled: true));

        if (_selectedIndex >= _actions.Count) _selectedIndex = 0;
    }

    public override void HandleInput(IInputManager input)
    {
        if (_interaction == null || _actions.Count == 0)
        {
            _ctx.RequestTransition(ScreenState.Playing);
            return;
        }

        if (_subMode == SubMode.Shop)
        {
            HandleShopInput(input);
            return;
        }

        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _ctx.RequestTransition(ScreenState.Playing);
            return;
        }

        if (InputHelpers.HandleListNavigation(input, ref _selectedIndex, _actions.Count))
            return;

        if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            ExecuteSelected();
        }
    }

    private void HandleShopInput(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _subMode = SubMode.ActionList;
            return;
        }

        if (input.IsActionPressed(InputAction.OpenInventory))
        {
            _shopSellMode = !_shopSellMode;
            _shopSelected = 0;
            _shopScroll = 0;
            return;
        }

        int count = _shopSellMode
            ? NpcDialogueRenderer.GetSellableItemCount(_ctx.GameState)
            : (_shopDef?.Items.Length ?? 0);
        if (count <= 0) return;

        if (input.IsActionPressedOrRepeated(InputAction.MenuUp))
        {
            _shopSelected = Math.Max(0, _shopSelected - 1);
            if (_shopSelected < _shopScroll) _shopScroll = _shopSelected;
        }
        else if (input.IsActionPressedOrRepeated(InputAction.MenuDown))
        {
            _shopSelected = Math.Min(count - 1, _shopSelected + 1);
        }
        else if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            if (_shopSellMode) TrySell();
            else TryBuy();
        }
    }

    private void ExecuteSelected()
    {
        if (_interaction == null) return;
        if (_selectedIndex < 0 || _selectedIndex >= _actions.Count) return;
        var action = _actions[_selectedIndex];
        if (!action.Enabled) return;

        switch (action.Kind)
        {
            case NpcDialogueActionKind.AcceptOffer:
                SendQuestAction(ActionTypes.AcceptQuest, action.QuestNumericId);
                _ctx.RequestTransition(ScreenState.Playing);
                break;

            case NpcDialogueActionKind.TurnIn:
                SendQuestAction(ActionTypes.TurnInQuest, action.QuestNumericId);
                _ctx.RequestTransition(ScreenState.Playing);
                break;

            case NpcDialogueActionKind.OpenShop:
            {
                var role = (TownNpcRole)_interaction.NpcRole;
                _shopRole = role;
                _shopDef = GameData.Instance.Shops.GetByRole(role);
                _shopSellMode = false;
                _shopSelected = 0;
                _shopScroll = 0;
                _subMode = SubMode.Shop;
                break;
            }

            case NpcDialogueActionKind.Leave:
                _ctx.RequestTransition(ScreenState.Playing);
                break;
        }
    }

    private void SendQuestAction(int actionType, int questNumericId)
    {
        if (_ctx.Connection == null || _interaction == null) return;
        var msg = new ClientInputMsg
        {
            ActionType = actionType,
            TargetNpcEntityId = _interaction.NpcEntityId,
            TargetQuestId = questNumericId,
            Tick = _ctx.GameState.WorldTick,
        };
        _ = _ctx.Connection.SendInputAsync(msg);
    }

    protected override void RenderPanel(ISpriteRenderer renderer, int hudStartCol, int totalRows)
    {
        // Clamp shop scroll so selection is visible. We compute the list bounds the same way
        // the renderer does (header row, gold row, separator, footer); this must mirror the
        // renderer layout but keeping it here avoids mutating state during rendering.
        ClampShopScroll(totalRows);

        var vm = new NpcDialogueViewModel(
            _interaction,
            _actions,
            _selectedIndex,
            _subMode == SubMode.Shop,
            _shopDef,
            _shopSellMode,
            _shopSelected,
            _shopScroll);

        _dialogueRenderer.Render(renderer, _ctx.GameState, vm, hudStartCol, totalRows);
    }

    private void ClampShopScroll(int totalRows)
    {
        if (_subMode != SubMode.Shop) return;

        // Layout mirror: innerTop=0, header+gold+separator = 3 rows, footer takes 2 rows,
        // detail pane = 1 row, plus separator above detail = 1 row. So list occupies
        // totalRows - 2 (footer) - 1 (separator above footer) - 1 (detail row) - 1 (separator above detail) - 3 (top).
        int listTop = 3;
        int footerRow = totalRows - 2;
        int detailRow = footerRow - 2;
        int listBottom = detailRow - 1;
        int visibleRows = Math.Max(1, listBottom - listTop);

        int itemCount = _shopSellMode
            ? NpcDialogueRenderer.GetSellableItemCount(_ctx.GameState)
            : (_shopDef?.Items.Length ?? 0);

        if (_shopSelected >= _shopScroll + visibleRows)
            _shopScroll = _shopSelected - visibleRows + 1;
        if (_shopScroll > Math.Max(0, itemCount - visibleRows))
            _shopScroll = Math.Max(0, itemCount - visibleRows);
        if (_shopScroll < 0) _shopScroll = 0;
    }

    private void TryBuy()
    {
        if (_ctx.Connection == null || _shopDef == null) return;
        if (_shopSelected < 0 || _shopSelected >= _shopDef.Items.Length) return;

        var entry = _shopDef.Items[_shopSelected];
        if (NpcDialogueRenderer.GetPlayerGoldCount(_ctx.GameState) < entry.Price) return;

        var msg = new ClientInputMsg
        {
            ActionType = ActionTypes.BuyItem,
            ItemSlot = _shopSelected,
            TargetSlot = (int)_shopRole,
            Tick = _ctx.GameState.WorldTick,
        };
        _ = _ctx.Connection.SendInputAsync(msg);
    }

    private void TrySell()
    {
        if (_ctx.Connection == null) return;
        int invSlot = NpcDialogueRenderer.GetSellableSlotIndex(_ctx.GameState, _shopSelected);
        if (invSlot < 0) return;

        var msg = new ClientInputMsg
        {
            ActionType = ActionTypes.SellItem,
            ItemSlot = invSlot,
            TargetSlot = (int)_shopRole,
            Tick = _ctx.GameState.WorldTick,
        };
        _ = _ctx.Connection.SendInputAsync(msg);
    }
}
