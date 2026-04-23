using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Rendering.Hud;

/// <summary>
/// Renders the NPC dialogue HUD panel: action list / detail pane, and the shop sub-view (buy/sell).
/// All input / mode state is owned by the screen; the renderer is a pure view given a
/// <see cref="NpcDialogueViewModel"/> snapshot.
/// </summary>
public sealed class NpcDialogueRenderer
{
    private static readonly Color4 SelColor = new(255, 255, 80, 255);
    private static readonly Color4 DisabledColor = new(120, 120, 120, 255);
    private static readonly Color4 QuestColor = new(180, 200, 255, 255);
    private static readonly Color4 ReadyColor = new(140, 255, 140, 255);
    private static readonly Color4 GoldColor = new(255, 215, 0, 255);
    private static readonly Color4 CantAffordColor = new(120, 120, 120, 255);

    public void Render(ISpriteRenderer r, ClientGameState state, in NpcDialogueViewModel vm, int hudStartCol, int totalRows)
    {
        if (vm.Interaction == null) return;

        HudPanelChrome.DrawBorder(r, hudStartCol, totalRows);

        int innerX = hudStartCol + 1;
        int innerW = AsciiDraw.HudColumns - 1;
        int innerTop = 0;
        int footerRow = totalRows - 2;

        if (vm.IsShopView)
        {
            RenderShopSubView(r, state, vm, innerX, innerW, innerTop, footerRow);
            return;
        }

        RenderActionList(r, vm, innerX, innerW, innerTop, footerRow);
    }

    private static void RenderActionList(ISpriteRenderer r, in NpcDialogueViewModel vm, int innerX, int innerW, int innerTop, int footerRow)
    {
        var interaction = vm.Interaction!;

        // Footer hint
        string hint = "[Enter] OK";
        if (hint.Length > innerW) hint = hint[..innerW];
        AsciiDraw.DrawString(r, innerX, footerRow, hint, RenderingTheme.Dim);
        AsciiDraw.DrawHudSeparator(r, innerX, footerRow - 1, innerW);

        // Header: NPC name
        int row = innerTop;
        string title = interaction.NpcName;
        if (title.Length > innerW) title = title[..innerW];
        AsciiDraw.DrawString(r, innerX, row++, title, RenderingTheme.Title);
        AsciiDraw.DrawString(r, innerX + innerW - 5, innerTop, "[ESC]", RenderingTheme.Dim);
        AsciiDraw.DrawHudSeparator(r, innerX, row++, innerW);

        int listBottomExclusive = footerRow - 1;

        // Flavor text (wrapped) — clamp to at most 3 lines so the list has room.
        var flavorLines = RenderingHelpers.WrapText(interaction.FlavorText, innerW).ToList();
        int maxFlavor = Math.Min(flavorLines.Count, 3);
        for (int i = 0; i < maxFlavor && row < listBottomExclusive; i++)
        {
            AsciiDraw.DrawString(r, innerX, row++, flavorLines[i], RenderingTheme.Normal);
        }

        if (maxFlavor > 0 && row < listBottomExclusive)
        {
            AsciiDraw.DrawHudSeparator(r, innerX, row++, innerW);
        }

        // Action list — budget roughly half the remaining space.
        int remaining = listBottomExclusive - row;
        int actionRows = Math.Min(vm.Actions.Count, Math.Max(2, remaining / 2));
        if (actionRows > remaining - 1) actionRows = Math.Max(1, remaining - 1);

        for (int i = 0; i < vm.Actions.Count && actionRows > 0 && row < listBottomExclusive; i++, actionRows--)
        {
            var a = vm.Actions[i];
            bool sel = i == vm.SelectedActionIndex;
            string prefix = sel ? "\u25ba" : " ";
            string text = prefix + a.Label;
            if (text.Length > innerW) text = text[..innerW];

            Color4 color;
            if (sel) color = SelColor;
            else if (!a.Enabled) color = DisabledColor;
            else if (a.Kind == NpcDialogueActionKind.TurnIn) color = ReadyColor;
            else if (a.Kind == NpcDialogueActionKind.AcceptOffer) color = QuestColor;
            else color = RenderingTheme.Normal;

            AsciiDraw.DrawString(r, innerX, row++, text, color);
        }

        if (row < listBottomExclusive)
        {
            AsciiDraw.DrawHudSeparator(r, innerX, row++, innerW);
        }

        if (row < listBottomExclusive)
        {
            RenderSelectedDetail(r, vm, innerX, innerW, row, listBottomExclusive);
        }
    }

    private static void RenderSelectedDetail(ISpriteRenderer r, in NpcDialogueViewModel vm, int col, int innerW, int startRow, int endRow)
    {
        var interaction = vm.Interaction!;
        if (vm.SelectedActionIndex < 0 || vm.SelectedActionIndex >= vm.Actions.Count) return;
        var action = vm.Actions[vm.SelectedActionIndex];

        int row = startRow;
        switch (action.Kind)
        {
            case NpcDialogueActionKind.AcceptOffer:
            {
                var offer = FindOffer(interaction, action.QuestNumericId);
                if (offer == null) return;
                AsciiDraw.DrawString(r, col, row++, offer.Title, RenderingTheme.Title);
                if (row >= endRow) return;
                foreach (var line in RenderingHelpers.WrapText(offer.Description, innerW))
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
                    string descLine = "  - " + desc;

                    foreach (var line in RenderingHelpers.WrapText(descLine, innerW, 4))
                    {
                        if (row >= endRow) return;
                        AsciiDraw.DrawString(r, col, row++, line, ReadyColor);
                    }
                }
                if (row < endRow && offer.Rewards != null)
                {
                    row++;
                    var rewardLine = RewardLine(offer.Rewards);

                    foreach (var line in RenderingHelpers.WrapText(rewardLine, innerW, 8))
                    {
                        if (row >= endRow) return;
                        AsciiDraw.DrawString(r, col, row++, line, ReadyColor);
                    }
                }
                break;
            }
            case NpcDialogueActionKind.TurnIn:
            {
                var turnIn = FindTurnIn(interaction, action.QuestNumericId);
                if (turnIn == null) return;
                AsciiDraw.DrawString(r, col, row++, turnIn.Title, RenderingTheme.Title);
                if (row >= endRow) return;
                string status = turnIn.IsComplete
                    ? "All objectives complete!"
                    : "Objectives in progress...";
                AsciiDraw.DrawString(r, col, row++, status, turnIn.IsComplete ? ReadyColor : RenderingTheme.Dim);

                if (!string.IsNullOrEmpty(turnIn.CompletionText) && turnIn.IsComplete)
                {
                    foreach (var line in RenderingHelpers.WrapText(turnIn.CompletionText, innerW))
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
                    string fullLine = $"  {mark} {desc}";
                    foreach (var line in RenderingHelpers.WrapText(fullLine, innerW, 4))
                    {
                        if (row >= endRow) return;
                        var c = o.Current >= o.Target ? ReadyColor : RenderingTheme.Normal;
                        AsciiDraw.DrawString(r, col, row++, line, c);
                    }
                }
                break;
            }
            case NpcDialogueActionKind.OpenShop:
                AsciiDraw.DrawString(r, col, row, "Browse this merchant's wares.", RenderingTheme.Normal);
                break;
            case NpcDialogueActionKind.Leave:
                foreach (var line in RenderingHelpers.WrapText("Step away from the conversation.", innerW))
                {
                    if (row >= endRow) return;
                    AsciiDraw.DrawString(r, col, row++, line, RenderingTheme.Dim);
                }
                break;
        }
    }

    private static void RenderShopSubView(ISpriteRenderer r, ClientGameState state, in NpcDialogueViewModel vm, int innerX, int innerW, int innerTop, int footerRow)
    {
        var interaction = vm.Interaction!;

        // Footer hint — compact to fit the narrow HUD column.
        string hint = vm.ShopSellMode
            ? "[Enter] Sell  [I] Buy"
            : "[Enter] Buy   [I] Sell";
        if (hint.Length > innerW) hint = hint[..innerW];
        AsciiDraw.DrawString(r, innerX, footerRow, hint, RenderingTheme.Dim);
        AsciiDraw.DrawHudSeparator(r, innerX, footerRow - 1, innerW);

        int row = innerTop;

        // Header: shop/NPC name + mode
        string shopName = vm.ShopDef?.Name ?? interaction.NpcName;
        string mode = vm.ShopSellMode ? "SELL" : "BUY";
        string title = $"{shopName} [{mode}]";
        if (title.Length > innerW) title = title[..innerW];
        AsciiDraw.DrawString(r, innerX, row++, title, RenderingTheme.Title);
        AsciiDraw.DrawString(r, innerX + innerW - 5, innerTop, "[ESC]", RenderingTheme.Dim);

        // Gold line
        int gold = GetPlayerGoldCount(state);
        AsciiDraw.DrawString(r, innerX, row++, $"Gold: {gold}", GoldColor);

        AsciiDraw.DrawHudSeparator(r, innerX, row++, innerW);

        int listTop = row;
        int detailRow = footerRow - 2;
        int listBottom = detailRow - 1;
        int visibleRows = Math.Max(1, listBottom - listTop);

        if (vm.ShopSellMode)
            RenderSellList(r, state, vm, innerX, innerW, listTop, listBottom, visibleRows);
        else
            RenderBuyList(r, vm, innerX, innerW, listTop, listBottom, visibleRows, gold);

        // Detail pane: price for selection.
        AsciiDraw.DrawHudSeparator(r, innerX, listBottom, innerW);
        RenderShopDetail(r, state, vm, innerX, innerW, detailRow);
    }

    private static void RenderBuyList(ISpriteRenderer r, in NpcDialogueViewModel vm, int col, int innerW, int rowStart, int rowEnd, int visibleRows, int gold)
    {
        if (vm.ShopDef == null) return;
        int itemCount = vm.ShopDef.Items.Length;
        int renderEnd = Math.Min(vm.ShopScroll + visibleRows, itemCount);

        bool showTop = vm.ShopScroll > 0;
        bool showBottom = vm.ShopScroll + visibleRows < itemCount;

        int row = rowStart;
        for (int i = vm.ShopScroll; i < renderEnd && row < rowEnd; i++, row++)
        {
            var entry = vm.ShopDef.Items[i];
            var def = GameData.Instance.Items.Get(entry.ItemId);
            if (def == null) continue;

            bool sel = i == vm.ShopSelected;
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

            if (i == vm.ShopScroll && showTop)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2191', RenderingTheme.Dim);
            if (i == renderEnd - 1 && showBottom)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2193', RenderingTheme.Dim);
        }
    }

    private static void RenderSellList(ISpriteRenderer r, ClientGameState state, in NpcDialogueViewModel vm, int col, int innerW, int rowStart, int rowEnd, int visibleRows)
    {
        var hud = state.PlayerState;
        if (hud == null) return;
        int goldId = GameData.Instance.Items.GetNumericId("gold_coin");
        int totalSellable = GetSellableItemCount(state);

        bool showTop = vm.ShopScroll > 0;
        bool showBottom = vm.ShopScroll + visibleRows < totalSellable;

        int sellableIdx = 0;
        int row = rowStart;
        for (int i = 0; i < hud.InventoryItems.Length && row < rowEnd; i++)
        {
            var item = hud.InventoryItems[i];
            if (item.ItemTypeId == goldId) continue;

            if (sellableIdx < vm.ShopScroll) { sellableIdx++; continue; }

            var def = GameData.Instance.Items.Get(item.ItemTypeId);
            if (def == null) { sellableIdx++; continue; }

            bool sel = sellableIdx == vm.ShopSelected;
            string prefix = sel ? "\u25ba" : " ";
            string name = def.Name ?? "???";
            if (item.StackCount > 1) name += $" x{item.StackCount}";
            int sellPrice = CalculateClientSellPrice(vm.ShopDef, def);
            string price = $"{sellPrice}g";
            int maxNameLen = innerW - price.Length - 2;
            if (name.Length > maxNameLen) name = name[..maxNameLen];
            string line = $"{prefix}{name}";
            int pad = innerW - line.Length - price.Length;
            if (pad > 0) line += new string(' ', pad);
            line += price;
            var color = sel ? RenderingTheme.InvSel : RenderingTheme.Item;
            AsciiDraw.DrawString(r, col, row, line, color);

            if (sellableIdx == vm.ShopScroll && showTop)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2191', RenderingTheme.Dim);
            if (row == rowEnd - 1 && showBottom)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2193', RenderingTheme.Dim);

            sellableIdx++;
            row++;
        }
    }

    private static void RenderShopDetail(ISpriteRenderer r, ClientGameState state, in NpcDialogueViewModel vm, int col, int innerW, int row)
    {
        var hud = state.PlayerState;
        if (hud == null) return;

        if (!vm.ShopSellMode && vm.ShopDef != null &&
            vm.ShopSelected >= 0 && vm.ShopSelected < vm.ShopDef.Items.Length)
        {
            var entry = vm.ShopDef.Items[vm.ShopSelected];
            var def = GameData.Instance.Items.Get(entry.ItemId);
            if (def != null)
            {
                string line = $"{def.Name}  Price: {entry.Price}g";
                if (line.Length > innerW) line = line[..innerW];
                AsciiDraw.DrawString(r, col, row, line, RenderingTheme.Dim);
            }
        }
        else if (vm.ShopSellMode)
        {
            int slot = GetSellableSlotIndex(state, vm.ShopSelected);
            if (slot >= 0 && slot < hud.InventoryItems.Length)
            {
                var def = GameData.Instance.Items.Get(hud.InventoryItems[slot].ItemTypeId);
                if (def != null)
                {
                    int p = CalculateClientSellPrice(vm.ShopDef, def);
                    string line = $"{def.Name}  Sell for: {p}g";
                    if (line.Length > innerW) line = line[..innerW];
                    AsciiDraw.DrawString(r, col, row, line, GoldColor);
                }
            }
        }
    }

    // ─── Helpers shared between rendering and screen actions ────────────────

    public static QuestOfferMsg? FindOffer(NpcInteractionMsg interaction, int questNumericId)
    {
        foreach (var o in interaction.QuestOffers)
            if (o.QuestNumericId == questNumericId) return o;
        return null;
    }

    public static QuestTurnInMsg? FindTurnIn(NpcInteractionMsg interaction, int questNumericId)
    {
        foreach (var t in interaction.QuestTurnIns)
            if (t.QuestNumericId == questNumericId) return t;
        return null;
    }

    public static int GetPlayerGoldCount(ClientGameState state)
    {
        var hud = state.PlayerState;
        if (hud == null) return 0;
        int goldId = GameData.Instance.Items.GetNumericId("gold_coin");
        int count = 0;
        foreach (var item in hud.InventoryItems)
            if (item.ItemTypeId == goldId) count += item.StackCount;
        return count;
    }

    public static int GetSellableItemCount(ClientGameState state)
    {
        var hud = state.PlayerState;
        if (hud == null) return 0;
        int goldId = GameData.Instance.Items.GetNumericId("gold_coin");
        int count = 0;
        foreach (var item in hud.InventoryItems)
            if (item.ItemTypeId != goldId) count++;
        return count;
    }

    public static int GetSellableSlotIndex(ClientGameState state, int sellableIndex)
    {
        var hud = state.PlayerState;
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

    public static int CalculateClientSellPrice(ShopDefinition? shopDef, ItemDefinition def)
    {
        if (shopDef == null) return 1;
        foreach (var entry in shopDef.Items)
            if (entry.ItemId == def.Id)
                return Math.Max(1, entry.Price * shopDef.SellPricePercent / 100);
        return 1;
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
}

/// <summary>Kinds of actions available in the NPC dialogue action list.</summary>
public enum NpcDialogueActionKind { AcceptOffer, TurnIn, OpenShop, Leave }

/// <summary>A single entry in the NPC dialogue action list.</summary>
public readonly struct NpcDialogueAction
{
    public NpcDialogueActionKind Kind { get; }
    public int QuestNumericId { get; }
    public string Label { get; }
    public bool Enabled { get; }

    public NpcDialogueAction(NpcDialogueActionKind kind, int questNumericId, string label, bool enabled)
    {
        Kind = kind;
        QuestNumericId = questNumericId;
        Label = label;
        Enabled = enabled;
    }
}

/// <summary>Immutable snapshot of the NPC dialogue screen's state passed to the renderer.</summary>
public readonly struct NpcDialogueViewModel
{
    public NpcInteractionMsg? Interaction { get; }
    public IReadOnlyList<NpcDialogueAction> Actions { get; }
    public int SelectedActionIndex { get; }
    public bool IsShopView { get; }
    public ShopDefinition? ShopDef { get; }
    public bool ShopSellMode { get; }
    public int ShopSelected { get; }
    public int ShopScroll { get; }

    public NpcDialogueViewModel(
        NpcInteractionMsg? interaction,
        IReadOnlyList<NpcDialogueAction> actions,
        int selectedActionIndex,
        bool isShopView,
        ShopDefinition? shopDef,
        bool shopSellMode,
        int shopSelected,
        int shopScroll)
    {
        Interaction = interaction;
        Actions = actions;
        SelectedActionIndex = selectedActionIndex;
        IsShopView = isShopView;
        ShopDef = shopDef;
        ShopSellMode = shopSellMode;
        ShopSelected = shopSelected;
        ShopScroll = shopScroll;
    }
}
