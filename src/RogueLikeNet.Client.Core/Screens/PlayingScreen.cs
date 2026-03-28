using System.Diagnostics;
using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Main gameplay screen — handles movement, combat, skills, quick slots, and renders the game world with HUD.
/// </summary>
public sealed class PlayingScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly GameWorldRenderer _worldRenderer;
    private readonly HudRenderer _hudRenderer;
    private readonly OverlayRenderer _overlayRenderer;

    // Movement hold-to-repeat
    private static readonly long MoveRepeatDelayTicks = Stopwatch.Frequency / 4; // 250 ms
    private static readonly long MoveRepeatDelayTicksFast = Stopwatch.Frequency / 20; // 50 ms (max speed)
    private InputAction? _heldMoveAction;
    private long _moveHeldSinceTicks;

    public ScreenState ScreenState => ScreenState.Playing;

    public PlayingScreen(ScreenContext ctx, GameWorldRenderer worldRenderer, HudRenderer hudRenderer,
        OverlayRenderer overlayRenderer)
    {
        _ctx = ctx;
        _worldRenderer = worldRenderer;
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

        if (input.IsActionPressed(InputAction.OpenChat))
        {
            _ctx.Chat.InputActive = true;
            _ctx.Chat.InputText = "";
            return;
        }

        // Debug key toggles (only in debug mode)
        if (_ctx.Debug.Enabled)
        {
            HandleDebugKeys(input);
        }

        ClientInputMsg? msg = null;

        bool maxSpeed = _ctx.Debug is { Enabled: true, MaxSpeed: true };
        long repeatDelay = maxSpeed ? MoveRepeatDelayTicksFast : MoveRepeatDelayTicks;

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
                            || now - _moveHeldSinceTicks >= repeatDelay;
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
                int steps = maxSpeed ? 4 : 1;
                for (int i = 0; i < steps; i++)
                {
                    msg = new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = dx, TargetY = dy };
                    SendInput(msg);
                }
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
                msg = new ClientInputMsg { ActionType = ActionTypes.UseQuickSlot, ItemSlot = 0 };
            else if (input.IsActionPressed(InputAction.UseItem2))
                msg = new ClientInputMsg { ActionType = ActionTypes.UseQuickSlot, ItemSlot = 1 };
            else if (input.IsActionPressed(InputAction.UseItem3))
                msg = new ClientInputMsg { ActionType = ActionTypes.UseQuickSlot, ItemSlot = 2 };
            else if (input.IsActionPressed(InputAction.UseItem4))
                msg = new ClientInputMsg { ActionType = ActionTypes.UseQuickSlot, ItemSlot = 3 };
            else if (input.IsActionPressed(InputAction.UseSkill1))
                msg = new ClientInputMsg { ActionType = ActionTypes.UseSkill, ItemSlot = 0, TargetX = 1, TargetY = 0 };
            else if (input.IsActionPressed(InputAction.UseSkill2))
                msg = new ClientInputMsg { ActionType = ActionTypes.UseSkill, ItemSlot = 1, TargetX = 1, TargetY = 0 };
        }

        if (msg != null)
            SendInput(msg);
    }

    private void SendInput(ClientInputMsg msg)
    {
        if (_ctx.Connection != null)
        {
            msg.Tick = _ctx.GameState.WorldTick;
            _ = _ctx.Connection.SendInputAsync(msg);
        }
    }

    private void HandleDebugKeys(IInputManager input)
    {
        string typed = input.TextInput;
        foreach (char c in typed)
        {
            switch (c)
            {
                case 'v' or 'V':
                    _ctx.Debug.VisibilityOff = !_ctx.Debug.VisibilityOff;
                    _ctx.GameState.DebugSeeAll = _ctx.Debug.VisibilityOff;
                    break;
                case 'c' or 'C':
                    _ctx.Debug.CollisionsOff = !_ctx.Debug.CollisionsOff;
                    SyncDebugToServer();
                    break;
                case 'h' or 'H':
                    _ctx.Debug.Invulnerable = !_ctx.Debug.Invulnerable;
                    SyncDebugToServer();
                    break;
                case 'l' or 'L':
                    _ctx.Debug.LightOff = !_ctx.Debug.LightOff;
                    break;
                case 'm' or 'M':
                    _ctx.Debug.MaxSpeed = !_ctx.Debug.MaxSpeed;
                    SyncDebugToServer();
                    break;
                case '+' or '=':
                    _ctx.Debug.ZoomLevel = Math.Max(-5, _ctx.Debug.ZoomLevel - 1);
                    SyncDebugToServer();
                    break;
                case '-' or '_':
                    _ctx.Debug.ZoomLevel = Math.Min(5, _ctx.Debug.ZoomLevel + 1);
                    SyncDebugToServer();
                    break;
                case '0':
                    _ctx.Debug.ZoomLevel = 0;
                    SyncDebugToServer();
                    break;
            }
        }
    }

    private void SyncDebugToServer()
    {
        _ctx.DebugSyncRequested?.Invoke();
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
        _hudRenderer.Render(renderer, _ctx.GameState, gameCols, totalRows);

        // Render particles
        int halfW = zoomedGameCols / 2;
        int halfH = zoomedRows / 2;
        _ctx.Particles.Render(renderer, _ctx.GameState.PlayerX, _ctx.GameState.PlayerY,
            halfW, halfH, shakeX, shakeY);

        _overlayRenderer.RenderChat(renderer, totalCols, totalRows, _ctx.Chat);
        _overlayRenderer.RenderPerformance(renderer, _ctx.Performance);
    }
}
