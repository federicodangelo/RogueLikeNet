using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Shop screen — allows buying items from and selling items to a shop NPC.
/// Two modes: Buy (browse shop inventory) and Sell (browse player inventory).
/// </summary>
public sealed class ShopScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly GameWorldRenderer _worldRenderer;
    private readonly OverlayRenderer _overlayRenderer;

    private readonly HudLayout _layout;
    private readonly HudSection _listSection;

    private bool _isSellMode;
    private ShopDefinition? _currentShop;
    private TownNpcRole _currentRole;

    private static readonly Color4 GoldColor = new(255, 215, 0, 255);
    private static readonly Color4 CantAffordColor = new(120, 120, 120, 255);

    public ScreenState ScreenState => ScreenState.Shop;

    public ShopScreen(ScreenContext ctx, GameWorldRenderer worldRenderer, OverlayRenderer overlayRenderer)
    {
        _ctx = ctx;
        _worldRenderer = worldRenderer;
        _overlayRenderer = overlayRenderer;

        _layout = new HudLayout();
        _layout.AddSection(new HudSection { Name = "ShopHeader", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 3 });
        _listSection = new HudSection { Name = "ShopList", Anchor = HudAnchor.Top, IsFixedHeight = false, Scrollable = true, AcceptsInput = true, UseScrollIndicators = true };
        _layout.AddSection(_listSection);
        _layout.AddSection(new HudSection { Name = "ShopDetail", Anchor = HudAnchor.Bottom, IsFixedHeight = true, FixedHeight = 6 });
        _layout.AddSection(new HudSection { Name = "ShopActions", Anchor = HudAnchor.Bottom, IsFixedHeight = true, FixedHeight = 4 });
        _layout.SetFocus(1);
    }

    /// <summary>Open the shop for a given NPC role.</summary>
    public void OpenShop(TownNpcRole role)
    {
        _currentRole = role;
        _currentShop = GameData.Instance.Shops.GetByRole(role);
        _isSellMode = false;
        _listSection.SelectedIndex = 0;
        _listSection.ScrollOffset = 0;
    }

    public void OnEnter()
    {
        _listSection.SelectedIndex = 0;
        _listSection.ScrollOffset = 0;
    }

    public void HandleInput(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _ctx.RequestTransition(ScreenState.Playing);
            return;
        }

        // Toggle Buy/Sell mode with inventory key
        if (input.IsActionPressed(InputAction.OpenInventory))
        {
            _isSellMode = !_isSellMode;
            _listSection.SelectedIndex = 0;
            _listSection.ScrollOffset = 0;
            return;
        }

        int count = _isSellMode ? GetSellableItemCount() : GetBuyableItemCount();

        if (input.IsActionPressedOrRepeated(InputAction.MenuUp)) _listSection.ScrollUp();
        else if (input.IsActionPressedOrRepeated(InputAction.MenuDown)) _listSection.ScrollDown(count);
        else if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            if (_isSellMode)
                TrySell();
            else
                TryBuy();
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

        RenderShopPanel(renderer, gameCols, totalRows);

        int halfW = zoomedGameCols / 2;
        int halfH = zoomedRows / 2;
        _ctx.Particles.Render(renderer, _ctx.GameState.PlayerX, _ctx.GameState.PlayerY,
            halfW, halfH, shakeX, shakeY, tileW, tileH);

        _overlayRenderer.RenderChat(renderer, totalCols, totalRows, _ctx.Chat);
        _overlayRenderer.RenderPerformance(renderer, _ctx.Performance, _ctx.Debug);
    }

    private void RenderShopPanel(ISpriteRenderer r, int hudStartCol, int totalRows)
    {
        float hx = hudStartCol * AsciiDraw.TileWidth;
        r.DrawRectScreen(hx, 0, AsciiDraw.HudColumns * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.HudBg);

        // Draw left border
        AsciiDraw.DrawChar(r, hudStartCol, 0, '\u252C', RenderingTheme.Border);
        for (int y = 1; y < totalRows - 1; y++)
            AsciiDraw.DrawChar(r, hudStartCol, y, '\u2502', RenderingTheme.Border);
        AsciiDraw.DrawChar(r, hudStartCol, totalRows - 1, '\u2534', RenderingTheme.Border);

        int col = hudStartCol + 1;
        int innerW = AsciiDraw.HudColumns - 1;

        var playerState = _ctx.GameState.PlayerState;
        if (playerState == null)
        {
            AsciiDraw.DrawString(r, col, 1, "No data", RenderingTheme.Dim);
            return;
        }

        _layout.ComputeLayout(totalRows);
        int goldCount = GetPlayerGoldCount();

        foreach (var section in _layout.Sections)
        {
            int row = section.StartRow;
            int maxRow = section.StartRow + section.RowCount;

            switch (section.Name)
            {
                case "ShopHeader":
                {
                    if (row >= maxRow) break;
                    string shopName = _currentShop?.Name ?? "Shop";
                    string modeLabel = _isSellMode ? "SELL" : "BUY";
                    string title = $"{shopName} [{modeLabel}]";
                    if (title.Length > innerW) title = title[..innerW];
                    AsciiDraw.DrawString(r, col, row, title, RenderingTheme.Title);
                    AsciiDraw.DrawString(r, col + innerW - 5, row, "[ESC]", RenderingTheme.Dim);
                    row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW);
                    break;
                }

                case "ShopList":
                    if (_isSellMode)
                        RenderSellList(r, col, innerW, row, maxRow, section);
                    else
                        RenderBuyList(r, col, innerW, row, maxRow, goldCount, section);
                    break;

                case "ShopDetail":
                {
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, $"Gold: {goldCount}", GoldColor); row++;

                    if (!_isSellMode && _currentShop != null)
                    {
                        int sel = _listSection.SelectedIndex;
                        if (sel >= 0 && sel < _currentShop.Items.Length)
                        {
                            var entry = _currentShop.Items[sel];
                            var def = GameData.Instance.Items.Get(entry.ItemId);
                            if (def != null && row < maxRow)
                            {
                                string info = $"Price: {entry.Price}g";
                                AsciiDraw.DrawString(r, col, row, info, RenderingTheme.Dim);
                            }
                        }
                    }
                    else if (_isSellMode)
                    {
                        int sel = _listSection.SelectedIndex;
                        int idx = GetSellableSlotIndex(sel);
                        if (idx >= 0 && playerState != null && idx < playerState.InventoryItems.Length && row < maxRow)
                        {
                            var item = playerState.InventoryItems[idx];
                            var def = GameData.Instance.Items.Get(item.ItemTypeId);
                            if (def != null)
                            {
                                int sellPrice = CalculateClientSellPrice(def);
                                AsciiDraw.DrawString(r, col, row, $"Sell for: {sellPrice}g", GoldColor);
                            }
                        }
                    }
                    break;
                }

                case "ShopActions":
                {
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;
                    if (row < maxRow) { AsciiDraw.DrawString(r, col, row, "[Enter] Confirm", RenderingTheme.Dim); row++; }
                    if (row < maxRow) { AsciiDraw.DrawString(r, col, row, "[I] Toggle Buy/Sell", RenderingTheme.Dim); row++; }
                    break;
                }
            }
        }
    }

    private void RenderBuyList(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        int goldCount, HudSection section)
    {
        if (_currentShop == null) return;

        int itemCount = _currentShop.Items.Length;
        int scrollOffset = section.ScrollOffset;
        int selectedIndex = section.SelectedIndex;
        int visibleRows = maxRow - row;

        bool showTopArrow = scrollOffset > 0;
        bool showBottomArrow = scrollOffset + visibleRows < itemCount;

        int renderEnd = Math.Min(scrollOffset + visibleRows, itemCount);
        for (int i = scrollOffset; i < renderEnd && row < maxRow; i++)
        {
            var entry = _currentShop.Items[i];
            var def = GameData.Instance.Items.Get(entry.ItemId);
            if (def == null) { row++; continue; }

            bool sel = i == selectedIndex;
            bool canAfford = goldCount >= entry.Price;

            string prefix = sel ? "\u25ba" : " ";
            string name = def.Name ?? entry.ItemId;
            string price = $"{entry.Price}g";

            // Truncate name to fit price at the end
            int maxNameLen = innerW - price.Length - 2;
            if (name.Length > maxNameLen) name = name[..maxNameLen];

            string line = $"{prefix}{name}";
            // Pad with spaces to push price to right
            int pad = innerW - line.Length - price.Length;
            if (pad > 0) line += new string(' ', pad);
            line += price;

            var color = sel ? RenderingTheme.InvSel : canAfford ? RenderingTheme.Item : CantAffordColor;
            AsciiDraw.DrawString(r, col, row, line, color);

            if (i == scrollOffset && showTopArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2191', RenderingTheme.Dim);
            if (i == renderEnd - 1 && showBottomArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2193', RenderingTheme.Dim);
            row++;
        }
    }

    private void RenderSellList(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        HudSection section)
    {
        var hud = _ctx.GameState.PlayerState;
        if (hud == null) return;

        int goldItemId = GameData.Instance.Items.GetNumericId("gold_coin");
        int selectedIndex = section.SelectedIndex;
        int scrollOffset = section.ScrollOffset;
        int visibleRows = maxRow - row;
        int totalSellable = GetSellableItemCount();

        bool showTopArrow = scrollOffset > 0;
        bool showBottomArrow = scrollOffset + visibleRows < totalSellable;

        int sellableIdx = 0;
        for (int i = 0; i < hud.InventoryItems.Length && row < maxRow; i++)
        {
            var item = hud.InventoryItems[i];
            if (item.ItemTypeId == goldItemId) continue;

            if (sellableIdx < scrollOffset) { sellableIdx++; continue; }

            var def = GameData.Instance.Items.Get(item.ItemTypeId);
            if (def == null) { sellableIdx++; continue; }

            bool sel = sellableIdx == selectedIndex;
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

            if (sellableIdx == scrollOffset && showTopArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2191', RenderingTheme.Dim);
            if (row == maxRow - 1 && showBottomArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2193', RenderingTheme.Dim);

            sellableIdx++;
            row++;
        }
    }

    private void TryBuy()
    {
        if (_ctx.Connection == null || _currentShop == null) return;
        int sel = _listSection.SelectedIndex;
        if (sel < 0 || sel >= _currentShop.Items.Length) return;

        var entry = _currentShop.Items[sel];
        int goldCount = GetPlayerGoldCount();
        if (goldCount < entry.Price) return;

        var msg = new ClientInputMsg
        {
            ActionType = ActionTypes.BuyItem,
            ItemSlot = sel,
            TargetSlot = (int)_currentRole,
            Tick = _ctx.GameState.WorldTick,
        };
        _ = _ctx.Connection.SendInputAsync(msg);
    }

    private void TrySell()
    {
        if (_ctx.Connection == null) return;
        var hud = _ctx.GameState.PlayerState;
        if (hud == null) return;

        int invSlot = GetSellableSlotIndex(_listSection.SelectedIndex);
        if (invSlot < 0) return;

        var msg = new ClientInputMsg
        {
            ActionType = ActionTypes.SellItem,
            ItemSlot = invSlot,
            TargetSlot = (int)_currentRole,
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
        {
            if (item.ItemTypeId == goldId)
                count += item.StackCount;
        }
        return count;
    }

    private int GetBuyableItemCount() => _currentShop?.Items.Length ?? 0;

    private int GetSellableItemCount()
    {
        var hud = _ctx.GameState.PlayerState;
        if (hud == null) return 0;

        int goldId = GameData.Instance.Items.GetNumericId("gold_coin");
        int count = 0;
        foreach (var item in hud.InventoryItems)
        {
            if (item.ItemTypeId != goldId)
                count++;
        }
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
        if (_currentShop == null) return 1;
        foreach (var entry in _currentShop.Items)
        {
            if (entry.ItemId == def.Id)
                return Math.Max(1, entry.Price * _currentShop.SellPricePercent / 100);
        }
        return 1;
    }
}
