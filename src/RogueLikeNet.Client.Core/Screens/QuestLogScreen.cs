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

        AsciiDraw.FillOverlay(renderer, totalCols, totalRows);
        RenderPanel(renderer, totalCols, totalRows);

        _overlayRenderer.RenderChat(renderer, totalCols, totalRows, _ctx.Chat);
        if (_ctx.Options.ShowStats)
            _overlayRenderer.RenderPerformance(renderer, gameCols, _ctx.Performance, _ctx.Debug);
    }

    private void RenderPanel(ISpriteRenderer r, int totalCols, int totalRows)
    {
        int w = Math.Min(70, totalCols - 4);
        int h = Math.Min(28, totalRows - 4);
        int x = (totalCols - w) / 2;
        int y = (totalRows - h) / 2;

        AsciiDraw.DrawBox(r, x, y, w, h, RenderingTheme.Border, RenderingTheme.OverlayBg);

        int innerX = x + 2;
        int innerW = w - 4;
        int row = y + 1;

        AsciiDraw.DrawString(r, innerX, row, "Quest Log", RenderingTheme.Title);
        row++;
        AsciiDraw.DrawHudSeparator(r, innerX, row, innerW);
        row++;

        var quests = _ctx.GameState.PlayerState?.Quests?.Active ?? [];
        if (quests.Length == 0)
        {
            AsciiDraw.DrawString(r, innerX, row, "No active quests.", RenderingTheme.Dim);
        }
        else
        {
            // Left column: quest titles. Right column: selected quest detail.
            int leftW = Math.Max(20, innerW / 2 - 1);
            int detailX = innerX + leftW + 2;
            int detailW = innerW - leftW - 2;

            int listStart = row;
            int listMax = y + h - 3;
            for (int i = 0; i < quests.Length && row < listMax; i++)
            {
                bool sel = i == _selectedIndex;
                var q = quests[i];
                bool complete = AllObjectivesComplete(q);
                string prefix = sel ? "\u25ba " : "  ";
                string text = prefix + q.Title;
                if (text.Length > leftW) text = text[..leftW];
                Color4 color = sel ? SelColor : complete ? ReadyColor : ProgressColor;
                AsciiDraw.DrawString(r, innerX, row, text, color);
                row++;
            }

            // Detail panel
            if (_selectedIndex >= 0 && _selectedIndex < quests.Length)
            {
                var q = quests[_selectedIndex];
                int drow = listStart;
                string title = q.Title;
                if (title.Length > detailW) title = title[..detailW];
                AsciiDraw.DrawString(r, detailX, drow, title, RenderingTheme.Title);
                drow++;

                if (!string.IsNullOrEmpty(q.GiverName) && drow < listMax)
                {
                    string giverLine = $"From: {q.GiverName}";
                    if (giverLine.Length > detailW) giverLine = giverLine[..detailW];
                    AsciiDraw.DrawString(r, detailX, drow++, giverLine, RenderingTheme.Dim);
                }

                if (drow < listMax)
                {
                    AsciiDraw.DrawHudSeparator(r, detailX, drow, detailW);
                    drow++;
                }

                foreach (var obj in q.Objectives)
                {
                    if (drow >= listMax) break;
                    string mark = obj.Current >= obj.Target ? "[x] " : "[ ] ";
                    string desc = string.IsNullOrEmpty(obj.Description)
                        ? $"{obj.Current}/{obj.Target}"
                        : $"{obj.Description} ({obj.Current}/{obj.Target})";
                    string line = mark + desc;
                    if (line.Length > detailW) line = line[..detailW];
                    Color4 c = obj.Current >= obj.Target ? ReadyColor : RenderingTheme.Normal;
                    AsciiDraw.DrawString(r, detailX, drow, line, c);
                    drow++;
                }

                if (AllObjectivesComplete(q) && !string.IsNullOrEmpty(q.GiverName) && drow < listMax)
                {
                    drow++;
                    if (drow < listMax)
                    {
                        string returnLine = $"Return to {q.GiverName}!";
                        if (returnLine.Length > detailW) returnLine = returnLine[..detailW];
                        AsciiDraw.DrawString(r, detailX, drow, returnLine, ReadyColor);
                    }
                }
            }
        }

        int footerRow = y + h - 2;
        string hint = "[\u2191/\u2193] Select  [X] Abandon  [Esc/Q] Close";
        if (hint.Length > innerW) hint = hint[..innerW];
        AsciiDraw.DrawString(r, innerX, footerRow, hint, RenderingTheme.Dim);
    }

    private static bool AllObjectivesComplete(ActiveQuestInfoMsg q)
    {
        if (q.Objectives.Length == 0) return false;
        foreach (var o in q.Objectives)
            if (o.Current < o.Target) return false;
        return true;
    }
}
