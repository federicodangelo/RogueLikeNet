using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Modal shown when bumping into a town NPC that has quest offers, quest
/// turn-ins, or a shop. Lets the player pick an action and dispatches the
/// corresponding <see cref="ClientInputMsg"/>.
/// </summary>
public sealed class NpcDialogueScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly GameWorldRenderer _worldRenderer;
    private readonly OverlayRenderer _overlayRenderer;

    private NpcInteractionMsg? _interaction;
    private int _selectedIndex;

    // Each entry is one menu action.
    private readonly List<DialogueAction> _actions = new();

    // Shop sub-view state — active when the player picks "Browse shop".
    private enum SubMode { ActionList, Shop }
    private SubMode _subMode;
    private ShopDefinition? _shopDef;
    private TownNpcRole _shopRole;
    private bool _shopSellMode;
    private int _shopSelected;
    private int _shopScroll;

    private enum DialogueActionKind { AcceptOffer, TurnIn, OpenShop, Leave }

    private readonly struct DialogueAction
    {
        public readonly DialogueActionKind Kind;
        public readonly int QuestNumericId;
        public readonly string Label;
        public readonly bool Enabled;

        public DialogueAction(DialogueActionKind kind, int questNumericId, string label, bool enabled)
        {
            Kind = kind;
            QuestNumericId = questNumericId;
            Label = label;
            Enabled = enabled;
        }
    }

    public ScreenState ScreenState => ScreenState.NpcDialogue;

    private static readonly Color4 SelColor = new(255, 255, 80, 255);
    private static readonly Color4 DisabledColor = new(120, 120, 120, 255);
    private static readonly Color4 QuestColor = new(180, 200, 255, 255);
    private static readonly Color4 ReadyColor = new(140, 255, 140, 255);
    private static readonly Color4 GoldColor = new(255, 215, 0, 255);
    private static readonly Color4 CantAffordColor = new(120, 120, 120, 255);

    public NpcDialogueScreen(ScreenContext ctx, GameWorldRenderer worldRenderer, OverlayRenderer overlayRenderer)
    {
        _ctx = ctx;
        _worldRenderer = worldRenderer;
        _overlayRenderer = overlayRenderer;
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
        // Rebuild in case player state changed between enqueue and show.
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
            _actions.Add(new DialogueAction(
                DialogueActionKind.TurnIn, turnIn.QuestNumericId, label, turnIn.IsComplete));
        }

        foreach (var offer in _interaction.QuestOffers)
        {
            _actions.Add(new DialogueAction(
                DialogueActionKind.AcceptOffer, offer.QuestNumericId, $"Accept: {offer.Title}", enabled: true));
        }

        if (_interaction.HasShop)
        {
            _actions.Add(new DialogueAction(
                DialogueActionKind.OpenShop, 0, "Browse shop", enabled: true));
        }

        _actions.Add(new DialogueAction(DialogueActionKind.Leave, 0, "Leave", enabled: true));

        if (_selectedIndex >= _actions.Count) _selectedIndex = 0;
    }

    public void HandleInput(IInputManager input)
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

        if (input.IsActionPressedOrRepeated(InputAction.MenuUp))
        {
            _selectedIndex = (_selectedIndex - 1 + _actions.Count) % _actions.Count;
        }
        else if (input.IsActionPressedOrRepeated(InputAction.MenuDown))
        {
            _selectedIndex = (_selectedIndex + 1) % _actions.Count;
        }
        else if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            ExecuteSelected();
        }
    }

    private void HandleShopInput(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            // Return to the action list view; stay inside the dialogue.
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

        int count = _shopSellMode ? GetSellableItemCount() : (_shopDef?.Items.Length ?? 0);
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
            case DialogueActionKind.AcceptOffer:
                SendQuestAction(ActionTypes.AcceptQuest, action.QuestNumericId);
                _ctx.RequestTransition(ScreenState.Playing);
                break;

            case DialogueActionKind.TurnIn:
                SendQuestAction(ActionTypes.TurnInQuest, action.QuestNumericId);
                _ctx.RequestTransition(ScreenState.Playing);
                break;

            case DialogueActionKind.OpenShop:
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

            case DialogueActionKind.Leave:
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

        var debug = _ctx.Debug;
        int tileW = debug.EffectiveTileWidth;
        int tileH = debug.EffectiveTileHeight;
        float fontScale = debug.EffectiveFontScale;

        int gamePixelW = gameCols * AsciiDraw.TileWidth;
        int gamePixelH = totalRows * AsciiDraw.TileHeight;
        int zoomedGameCols = Math.Max(1, gamePixelW / tileW);
        int zoomedRows = Math.Max(1, gamePixelH / tileH);

        renderer.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);
        bool debugLightOff = debug is { Enabled: true, LightOff: true };
        _worldRenderer.Render(renderer, _ctx.GameState, zoomedGameCols, zoomedRows, shakeX, shakeY, tileW, tileH, fontScale, debugLightOff);

        int halfW = zoomedGameCols / 2;
        int halfH = zoomedRows / 2;
        _ctx.Particles.Render(renderer, _ctx.GameState.PlayerX, _ctx.GameState.PlayerY,
            halfW, halfH, shakeX, shakeY, tileW, tileH);

        AsciiDraw.FillOverlay(renderer, totalCols, totalRows);
        RenderModal(renderer, totalCols, totalRows);

        _overlayRenderer.RenderChat(renderer, totalCols, totalRows, _ctx.Chat);
        if (_ctx.Options.ShowStats)
            _overlayRenderer.RenderPerformance(renderer, gameCols, _ctx.Performance, _ctx.Debug);
        if (_ctx.Options.ShowQuestTracker)
            _overlayRenderer.RenderQuestTracker(renderer, gameCols, totalRows, _ctx.GameState.PlayerState);
    }

    private void RenderModal(ISpriteRenderer r, int totalCols, int totalRows)
    {
        if (_interaction == null) return;

        int w = Math.Min(72, totalCols - 4);
        int h = Math.Min(30, totalRows - 4);
        int x = (totalCols - w) / 2;
        int y = (totalRows - h) / 2;

        AsciiDraw.DrawBox(r, x, y, w, h, RenderingTheme.Border, RenderingTheme.OverlayBg);

        int innerX = x + 2;
        int innerW = w - 4;
        int innerTop = y + 1;
        int innerBottom = y + h - 2;      // last usable row (footer sits on y+h-2)
        int footerRow = y + h - 2;

        if (_subMode == SubMode.Shop)
        {
            RenderShopSubView(r, innerX, innerW, innerTop, innerBottom, footerRow);
            return;
        }

        // Footer hint
        string hint = "[\u2191/\u2193] Select  [Enter] Confirm  [Esc] Leave";
        if (hint.Length > innerW) hint = hint[..innerW];
        AsciiDraw.DrawString(r, innerX, footerRow, hint, RenderingTheme.Dim);

        // Header: NPC name
        int row = innerTop;
        string title = _interaction.NpcName;
        if (title.Length > innerW) title = title[..innerW];
        AsciiDraw.DrawString(r, innerX, row++, title, RenderingTheme.Title);

        // Flavor text (wrapped) — clamp to at most 4 lines so the list has room.
        var flavorLines = WrapText(_interaction.FlavorText, innerW).ToList();
        int maxFlavor = Math.Min(flavorLines.Count, 4);
        for (int i = 0; i < maxFlavor; i++)
        {
            AsciiDraw.DrawString(r, innerX, row++, flavorLines[i], RenderingTheme.Normal);
        }

        // Separator between flavor and action list
        AsciiDraw.DrawHudSeparator(r, innerX, row++, innerW);

        // Action list (one row per action)
        for (int i = 0; i < _actions.Count && row < innerBottom - 1; i++)
        {
            var a = _actions[i];
            bool sel = i == _selectedIndex;
            string prefix = sel ? "\u25ba " : "  ";
            string text = prefix + a.Label;
            if (text.Length > innerW) text = text[..innerW];

            Color4 color;
            if (sel) color = SelColor;
            else if (!a.Enabled) color = DisabledColor;
            else if (a.Kind == DialogueActionKind.TurnIn) color = ReadyColor;
            else if (a.Kind == DialogueActionKind.AcceptOffer) color = QuestColor;
            else color = RenderingTheme.Normal;

            AsciiDraw.DrawString(r, innerX, row++, text, color);
        }

        // Separator between action list and detail pane
        if (row < innerBottom - 1)
        {
            AsciiDraw.DrawHudSeparator(r, innerX, row++, innerW);
        }

        // Detail pane for the selected quest fills the remaining space.
        if (row < innerBottom - 1)
        {
            RenderSelectedDetail(r, innerX, innerW, row, footerRow - 1);
        }
    }

    private void RenderSelectedDetail(ISpriteRenderer r, int col, int innerW, int startRow, int endRow)
    {
        if (_interaction == null) return;
        if (_selectedIndex < 0 || _selectedIndex >= _actions.Count) return;
        var action = _actions[_selectedIndex];

        int row = startRow;
        switch (action.Kind)
        {
            case DialogueActionKind.AcceptOffer:
            {
                var offer = FindOffer(action.QuestNumericId);
                if (offer == null) return;
                AsciiDraw.DrawString(r, col, row++, offer.Title, RenderingTheme.Title);
                if (row >= endRow) return;
                foreach (var line in WrapText(offer.Description, innerW))
                {
                    if (row >= endRow) return;
                    AsciiDraw.DrawString(r, col, row++, line, RenderingTheme.Normal);
                }
                if (row < endRow) row++;
                if (row < endRow)
                    AsciiDraw.DrawString(r, col, row++, "Objectives:", RenderingTheme.Dim);
                foreach (var o in offer.Objectives)
                {
                    if (row >= endRow) return;
                    string desc = string.IsNullOrEmpty(o.Description)
                        ? $"{o.Current}/{o.Target}"
                        : $"{o.Description} ({o.Current}/{o.Target})";
                    string line = "  - " + desc;
                    if (line.Length > innerW) line = line[..innerW];
                    AsciiDraw.DrawString(r, col, row++, line, RenderingTheme.Normal);
                }
                if (row < endRow && offer.Rewards != null)
                {
                    row++;
                    if (row < endRow) AsciiDraw.DrawString(r, col, row++, RewardLine(offer.Rewards), ReadyColor);
                }
                break;
            }
            case DialogueActionKind.TurnIn:
            {
                var turnIn = FindTurnIn(action.QuestNumericId);
                if (turnIn == null) return;
                AsciiDraw.DrawString(r, col, row++, turnIn.Title, RenderingTheme.Title);
                if (row >= endRow) return;
                string status = turnIn.IsComplete
                    ? "All objectives complete!"
                    : "Objectives in progress...";
                AsciiDraw.DrawString(r, col, row++, status, turnIn.IsComplete ? ReadyColor : RenderingTheme.Dim);

                if (!string.IsNullOrEmpty(turnIn.CompletionText) && turnIn.IsComplete)
                {
                    foreach (var line in WrapText(turnIn.CompletionText, innerW))
                    {
                        if (row >= endRow) return;
                        AsciiDraw.DrawString(r, col, row++, line, RenderingTheme.Normal);
                    }
                }

                if (row < endRow) row++;
                if (row < endRow)
                    AsciiDraw.DrawString(r, col, row++, "Objectives:", RenderingTheme.Dim);
                foreach (var o in turnIn.Objectives)
                {
                    if (row >= endRow) return;
                    string mark = o.Current >= o.Target ? "[x]" : "[ ]";
                    string desc = string.IsNullOrEmpty(o.Description)
                        ? $"{o.Current}/{o.Target}"
                        : $"{o.Description} ({o.Current}/{o.Target})";
                    string line = $"  {mark} {desc}";
                    if (line.Length > innerW) line = line[..innerW];
                    var c = o.Current >= o.Target ? ReadyColor : RenderingTheme.Normal;
                    AsciiDraw.DrawString(r, col, row++, line, c);
                }
                break;
            }
            case DialogueActionKind.OpenShop:
                AsciiDraw.DrawString(r, col, row, "Browse this merchant's wares.", RenderingTheme.Normal);
                break;
            case DialogueActionKind.Leave:
                AsciiDraw.DrawString(r, col, row, "Step away from the conversation.", RenderingTheme.Dim);
                break;
        }
    }

    private void RenderShopSubView(ISpriteRenderer r, int innerX, int innerW, int innerTop, int innerBottom, int footerRow)
    {
        if (_interaction == null) return;

        // Footer hint
        string hint = _shopSellMode
            ? "[\u2191/\u2193] Select  [Enter] Sell  [I] Buy mode  [Esc] Back"
            : "[\u2191/\u2193] Select  [Enter] Buy   [I] Sell mode [Esc] Back";
        if (hint.Length > innerW) hint = hint[..innerW];
        AsciiDraw.DrawString(r, innerX, footerRow, hint, RenderingTheme.Dim);

        int row = innerTop;

        // Header: shop/NPC name + mode
        string shopName = _shopDef?.Name ?? _interaction.NpcName;
        string mode = _shopSellMode ? "SELL" : "BUY";
        string title = $"{shopName} [{mode}]";
        if (title.Length > innerW) title = title[..innerW];
        AsciiDraw.DrawString(r, innerX, row++, title, RenderingTheme.Title);

        // Gold line
        int gold = GetPlayerGoldCount();
        AsciiDraw.DrawString(r, innerX, row++, $"Gold: {gold}", GoldColor);

        AsciiDraw.DrawHudSeparator(r, innerX, row++, innerW);

        // List area fills the rest; reserve the last 2 rows for detail + footer.
        int listTop = row;
        int detailRow = footerRow - 1; // single-line detail pane just above footer
        int listBottom = detailRow - 1; // separator
        int visibleRows = Math.Max(1, listBottom - listTop);

        // Clamp scroll so the selection is visible.
        int itemCount = _shopSellMode ? GetSellableItemCount() : (_shopDef?.Items.Length ?? 0);
        if (_shopSelected >= _shopScroll + visibleRows)
            _shopScroll = _shopSelected - visibleRows + 1;
        if (_shopScroll > Math.Max(0, itemCount - visibleRows))
            _shopScroll = Math.Max(0, itemCount - visibleRows);
        if (_shopScroll < 0) _shopScroll = 0;

        if (_shopSellMode)
            RenderSellList(r, innerX, innerW, listTop, listBottom, visibleRows);
        else
            RenderBuyList(r, innerX, innerW, listTop, listBottom, visibleRows, gold);

        // Detail pane: price for selection.
        AsciiDraw.DrawHudSeparator(r, innerX, listBottom, innerW);
        RenderShopDetail(r, innerX, innerW, detailRow);
    }

    private void RenderBuyList(ISpriteRenderer r, int col, int innerW, int rowStart, int rowEnd, int visibleRows, int gold)
    {
        if (_shopDef == null) return;
        int itemCount = _shopDef.Items.Length;
        int renderEnd = Math.Min(_shopScroll + visibleRows, itemCount);

        bool showTop = _shopScroll > 0;
        bool showBottom = _shopScroll + visibleRows < itemCount;

        int row = rowStart;
        for (int i = _shopScroll; i < renderEnd && row < rowEnd; i++, row++)
        {
            var entry = _shopDef.Items[i];
            var def = GameData.Instance.Items.Get(entry.ItemId);
            if (def == null) continue;

            bool sel = i == _shopSelected;
            bool canAfford = gold >= entry.Price;
            string prefix = sel ? "\u25ba" : " ";
            string name = def.Name ?? entry.ItemId;
            string price = $"{entry.Price}g";
            int maxNameLen = innerW - price.Length - 2;
            if (name.Length > maxNameLen) name = name[..maxNameLen];
            string line = $"{prefix}{name}";
            int pad = innerW - line.Length - price.Length;
            if (pad > 0) line += new string(' ', pad);
            line += price;
            var color = sel ? RenderingTheme.InvSel : canAfford ? RenderingTheme.Item : CantAffordColor;
            AsciiDraw.DrawString(r, col, row, line, color);

            if (i == _shopScroll && showTop)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2191', RenderingTheme.Dim);
            if (i == renderEnd - 1 && showBottom)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2193', RenderingTheme.Dim);
        }
    }

    private void RenderSellList(ISpriteRenderer r, int col, int innerW, int rowStart, int rowEnd, int visibleRows)
    {
        var hud = _ctx.GameState.PlayerState;
        if (hud == null) return;
        int goldId = GameData.Instance.Items.GetNumericId("gold_coin");
        int totalSellable = GetSellableItemCount();

        bool showTop = _shopScroll > 0;
        bool showBottom = _shopScroll + visibleRows < totalSellable;

        int sellableIdx = 0;
        int row = rowStart;
        for (int i = 0; i < hud.InventoryItems.Length && row < rowEnd; i++)
        {
            var item = hud.InventoryItems[i];
            if (item.ItemTypeId == goldId) continue;

            if (sellableIdx < _shopScroll) { sellableIdx++; continue; }

            var def = GameData.Instance.Items.Get(item.ItemTypeId);
            if (def == null) { sellableIdx++; continue; }

            bool sel = sellableIdx == _shopSelected;
            string prefix = sel ? "\u25ba" : " ";
            string name = def.Name ?? "???";
            if (item.StackCount > 1) name += $" x{item.StackCount}";
            int sellPrice = CalculateClientSellPrice(def);
            string price = $"{sellPrice}g";
            int maxNameLen = innerW - price.Length - 2;
            if (name.Length > maxNameLen) name = name[..maxNameLen];
            string line = $"{prefix}{name}";
            int pad = innerW - line.Length - price.Length;
            if (pad > 0) line += new string(' ', pad);
            line += price;
            var color = sel ? RenderingTheme.InvSel : RenderingTheme.Item;
            AsciiDraw.DrawString(r, col, row, line, color);

            if (sellableIdx == _shopScroll && showTop)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2191', RenderingTheme.Dim);
            if (row == rowEnd - 1 && showBottom)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2193', RenderingTheme.Dim);

            sellableIdx++;
            row++;
        }
    }

    private void RenderShopDetail(ISpriteRenderer r, int col, int innerW, int row)
    {
        var hud = _ctx.GameState.PlayerState;
        if (hud == null) return;

        if (!_shopSellMode && _shopDef != null &&
            _shopSelected >= 0 && _shopSelected < _shopDef.Items.Length)
        {
            var entry = _shopDef.Items[_shopSelected];
            var def = GameData.Instance.Items.Get(entry.ItemId);
            if (def != null)
            {
                string line = $"{def.Name}  Price: {entry.Price}g";
                if (line.Length > innerW) line = line[..innerW];
                AsciiDraw.DrawString(r, col, row, line, RenderingTheme.Dim);
            }
        }
        else if (_shopSellMode)
        {
            int slot = GetSellableSlotIndex(_shopSelected);
            if (slot >= 0 && slot < hud.InventoryItems.Length)
            {
                var def = GameData.Instance.Items.Get(hud.InventoryItems[slot].ItemTypeId);
                if (def != null)
                {
                    int p = CalculateClientSellPrice(def);
                    string line = $"{def.Name}  Sell for: {p}g";
                    if (line.Length > innerW) line = line[..innerW];
                    AsciiDraw.DrawString(r, col, row, line, GoldColor);
                }
            }
        }
    }

    private QuestOfferMsg? FindOffer(int questNumericId)
    {
        if (_interaction == null) return null;
        foreach (var o in _interaction.QuestOffers)
            if (o.QuestNumericId == questNumericId) return o;
        return null;
    }

    private QuestTurnInMsg? FindTurnIn(int questNumericId)
    {
        if (_interaction == null) return null;
        foreach (var t in _interaction.QuestTurnIns)
            if (t.QuestNumericId == questNumericId) return t;
        return null;
    }

    private static string RewardLine(QuestRewardInfoMsg rewards)
    {
        var parts = new List<string>();
        if (rewards.Experience > 0) parts.Add($"{rewards.Experience} XP");
        if (rewards.Gold > 0) parts.Add($"{rewards.Gold} gold");
        foreach (var item in rewards.Items)
        {
            var def = GameData.Instance.Items.Get(item.ItemTypeId);
            string name = def?.Name ?? "item";
            parts.Add(item.StackCount > 1 ? $"{name} x{item.StackCount}" : name);
        }
        if (parts.Count == 0) return "Reward: none";
        return "Reward: " + string.Join(", ", parts);
    }

    private static IEnumerable<string> WrapText(string text, int width)
    {
        if (string.IsNullOrEmpty(text) || width <= 0) yield break;
        var words = text.Split(' ');
        var line = new System.Text.StringBuilder();
        foreach (var w in words)
        {
            if (line.Length == 0)
            {
                line.Append(w);
            }
            else if (line.Length + 1 + w.Length <= width)
            {
                line.Append(' ').Append(w);
            }
            else
            {
                yield return line.ToString();
                line.Clear();
                line.Append(w);
            }
        }
        if (line.Length > 0) yield return line.ToString();
    }

    // ─── Shop sub-view helpers ─────────────────────────────────────────────

    private void TryBuy()
    {
        if (_ctx.Connection == null || _shopDef == null) return;
        if (_shopSelected < 0 || _shopSelected >= _shopDef.Items.Length) return;

        var entry = _shopDef.Items[_shopSelected];
        if (GetPlayerGoldCount() < entry.Price) return;

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
        int invSlot = GetSellableSlotIndex(_shopSelected);
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

    private int GetPlayerGoldCount()
    {
        var hud = _ctx.GameState.PlayerState;
        if (hud == null) return 0;
        int goldId = GameData.Instance.Items.GetNumericId("gold_coin");
        int count = 0;
        foreach (var item in hud.InventoryItems)
            if (item.ItemTypeId == goldId) count += item.StackCount;
        return count;
    }

    private int GetSellableItemCount()
    {
        var hud = _ctx.GameState.PlayerState;
        if (hud == null) return 0;
        int goldId = GameData.Instance.Items.GetNumericId("gold_coin");
        int count = 0;
        foreach (var item in hud.InventoryItems)
            if (item.ItemTypeId != goldId) count++;
        return count;
    }

    private int GetSellableSlotIndex(int sellableIndex)
    {
        var hud = _ctx.GameState.PlayerState;
        if (hud == null || sellableIndex < 0) return -1;
        int goldId = GameData.Instance.Items.GetNumericId("gold_coin");
        int current = 0;
        for (int i = 0; i < hud.InventoryItems.Length; i++)
        {
            if (hud.InventoryItems[i].ItemTypeId == goldId) continue;
            if (current == sellableIndex) return i;
            current++;
        }
        return -1;
    }

    private int CalculateClientSellPrice(ItemDefinition def)
    {
        if (_shopDef == null) return 1;
        foreach (var entry in _shopDef.Items)
            if (entry.ItemId == def.Id)
                return Math.Max(1, entry.Price * _shopDef.SellPricePercent / 100);
        return 1;
    }
}
