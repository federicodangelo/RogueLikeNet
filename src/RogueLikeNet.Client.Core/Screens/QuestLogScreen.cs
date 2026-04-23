using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Quest journal — lists active quests and their objective progress.
/// Opened via the OpenQuestLog action (Q key) and closed with Esc/Q.
/// Allows abandoning a quest with X.
/// </summary>
public sealed class QuestLogScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly GameWorldRenderer _worldRenderer;
    private readonly OverlayRenderer _overlayRenderer;

    private int _selectedIndex;

    public ScreenState ScreenState => ScreenState.QuestLog;

    private static readonly Color4 SelColor = new(255, 255, 80, 255);
    private static readonly Color4 ReadyColor = new(140, 255, 140, 255);
    private static readonly Color4 ProgressColor = new(180, 200, 255, 255);

    public QuestLogScreen(ScreenContext ctx, GameWorldRenderer worldRenderer, OverlayRenderer overlayRenderer)
    {
        _ctx = ctx;
        _worldRenderer = worldRenderer;
        _overlayRenderer = overlayRenderer;
    }

    public void OnEnter()
    {
        _selectedIndex = 0;
    }

    public void HandleInput(IInputManager input)
    {
        var quests = _ctx.GameState.PlayerState?.Quests?.Active ?? [];

        if (input.IsActionPressed(InputAction.MenuBack) || input.IsActionPressed(InputAction.OpenQuestLog))
        {
            _ctx.RequestTransition(ScreenState.Playing);
            return;
        }

        if (input.IsActionPressed(InputAction.OpenInventory))
        {
            _ctx.RequestTransition(ScreenState.Inventory);
            return;
        }

        if (input.IsActionPressed(InputAction.OpenCrafting))
        {
            _ctx.RequestTransition(ScreenState.Crafting);
            return;
        }

        if (quests.Length == 0) return;

        if (input.IsActionPressedOrRepeated(InputAction.MenuUp))
            _selectedIndex = (_selectedIndex - 1 + quests.Length) % quests.Length;
        else if (input.IsActionPressedOrRepeated(InputAction.MenuDown))
            _selectedIndex = (_selectedIndex + 1) % quests.Length;
        else if (input.IsActionPressed(InputAction.Drop))
        {
            // Abandon selected quest
            var q = quests[_selectedIndex];
            if (_ctx.Connection != null)
            {
                var msg = new ClientInputMsg
                {
                    ActionType = ActionTypes.AbandonQuest,
                    TargetQuestId = q.QuestNumericId,
                    Tick = _ctx.GameState.WorldTick,
                };
                _ = _ctx.Connection.SendInputAsync(msg);
            }
            if (_selectedIndex >= quests.Length - 1 && _selectedIndex > 0) _selectedIndex--;
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

        RenderPanel(renderer, gameCols, totalRows);

        int halfW = zoomedGameCols / 2;
        int halfH = zoomedRows / 2;
        _ctx.Particles.Render(renderer, _ctx.GameState.PlayerX, _ctx.GameState.PlayerY,
            halfW, halfH, shakeX, shakeY, tileW, tileH);

        _overlayRenderer.RenderChat(renderer, totalCols, totalRows, _ctx.Chat);
        if (_ctx.Options.ShowStats)
            _overlayRenderer.RenderPerformance(renderer, gameCols, _ctx.Performance, _ctx.Debug);
    }

    private void RenderPanel(ISpriteRenderer r, int hudStartCol, int totalRows)
    {
        // Background + vertical separator, matching Inventory/Crafting HUD panels.
        float hx = hudStartCol * AsciiDraw.TileWidth;
        r.DrawRectScreen(hx, 0, AsciiDraw.HudColumns * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.HudBg);

        AsciiDraw.DrawChar(r, hudStartCol, 0, '\u252C', RenderingTheme.Border);
        for (int y = 1; y < totalRows - 1; y++)
            AsciiDraw.DrawChar(r, hudStartCol, y, '\u2502', RenderingTheme.Border);
        AsciiDraw.DrawChar(r, hudStartCol, totalRows - 1, '\u2534', RenderingTheme.Border);

        int col = hudStartCol + 1;
        int innerW = AsciiDraw.HudColumns - 1;

        int row = 0;

        // Header
        AsciiDraw.DrawString(r, col, row, "QUEST LOG", RenderingTheme.Title);
        AsciiDraw.DrawString(r, col + innerW - 5, row, "[ESC]", RenderingTheme.Dim);
        row++;
        AsciiDraw.DrawHudSeparator(r, col, row, innerW);
        row++;

        // Footer (bottom two rows)
        int footerRow = totalRows - 2;
        string hint = "[X] Abandon";
        if (hint.Length > innerW) hint = hint[..innerW];
        AsciiDraw.DrawString(r, col, footerRow, hint, RenderingTheme.Dim);
        AsciiDraw.DrawHudSeparator(r, col, footerRow - 1, innerW);

        int bottomLimit = footerRow - 1; // exclusive

        var quests = _ctx.GameState.PlayerState?.Quests?.Active ?? [];
        if (quests.Length == 0)
        {
            AsciiDraw.DrawString(r, col, row, "No active quests.", RenderingTheme.Dim);
            return;
        }

        // Split remaining vertical space: list on top, detail below.
        int available = bottomLimit - row;
        if (available <= 0) return;

        // Give the list at most half the space, but always show at least the selected area.
        int listRows = Math.Min(quests.Length, Math.Max(3, available / 2));
        if (listRows > available - 2) listRows = Math.Max(1, available - 2);

        // Clamp selection and compute scroll.
        if (_selectedIndex < 0) _selectedIndex = 0;
        if (_selectedIndex >= quests.Length) _selectedIndex = quests.Length - 1;

        int scrollOffset = 0;
        if (quests.Length > listRows)
        {
            scrollOffset = _selectedIndex - listRows / 2;
            if (scrollOffset < 0) scrollOffset = 0;
            if (scrollOffset > quests.Length - listRows) scrollOffset = quests.Length - listRows;
        }

        int listEnd = Math.Min(scrollOffset + listRows, quests.Length);
        bool showTopArrow = scrollOffset > 0;
        bool showBottomArrow = listEnd < quests.Length;

        int listStartRow = row;
        for (int i = scrollOffset; i < listEnd; i++)
        {
            bool sel = i == _selectedIndex;
            var q = quests[i];
            bool complete = AllObjectivesComplete(q);
            string prefix = sel ? "\u25ba" : " ";
            string text = prefix + q.Title;
            if (text.Length > innerW - 1) text = text[..(innerW - 1)];
            Color4 color = sel ? SelColor : complete ? ReadyColor : ProgressColor;
            AsciiDraw.DrawString(r, col, row, text, color);

            if (i == scrollOffset && showTopArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2191', RenderingTheme.Dim);
            if (i == listEnd - 1 && showBottomArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2193', RenderingTheme.Dim);
            row++;
        }

        // Separator between list and detail
        if (row < bottomLimit)
        {
            AsciiDraw.DrawHudSeparator(r, col, row, innerW);
            row++;
        }

        // Detail pane
        if (_selectedIndex >= 0 && _selectedIndex < quests.Length && row < bottomLimit)
        {
            var q = quests[_selectedIndex];
            string title = q.Title;
            if (title.Length > innerW) title = title[..innerW];
            AsciiDraw.DrawString(r, col, row++, title, RenderingTheme.Title);

            if (!string.IsNullOrEmpty(q.GiverName) && row < bottomLimit)
            {
                string giverLine = $"From: {q.GiverName}";
                if (giverLine.Length > innerW) giverLine = giverLine[..innerW];
                AsciiDraw.DrawString(r, col, row++, giverLine, RenderingTheme.Dim);
            }

            foreach (var obj in q.Objectives)
            {
                if (row >= bottomLimit) break;
                string mark = obj.Current >= obj.Target ? "[x] " : "[ ] ";
                string desc = string.IsNullOrEmpty(obj.Description)
                    ? $"{obj.Current}/{obj.Target}"
                    : $"{obj.Description} ({obj.Current}/{obj.Target})";
                string line = mark + desc;
                if (line.Length > innerW) line = line[..innerW];
                Color4 c = obj.Current >= obj.Target ? ReadyColor : RenderingTheme.Normal;
                AsciiDraw.DrawString(r, col, row++, line, c);
            }

            if (AllObjectivesComplete(q) && !string.IsNullOrEmpty(q.GiverName) && row < bottomLimit)
            {
                string returnLine = $"Return to {q.GiverName}!";
                if (returnLine.Length > innerW) returnLine = returnLine[..innerW];
                AsciiDraw.DrawString(r, col, row++, returnLine, ReadyColor);
            }
        }
    }

    private static bool AllObjectivesComplete(ActiveQuestInfoMsg q)
    {
        if (q.Objectives.Length == 0) return false;
        foreach (var o in q.Objectives)
            if (o.Current < o.Target) return false;
        return true;
    }
}
