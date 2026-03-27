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

        ClientInputMsg? msg = null;

        // Determine held movement direction, applying a 250ms initial-repeat delay
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
                msg = activeMoveAction.Value switch
                {
                    InputAction.MoveUp => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 0, TargetY = -1 },
                    InputAction.MoveDown => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 0, TargetY = 1 },
                    InputAction.MoveLeft => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = -1, TargetY = 0 },
                    InputAction.MoveRight => new ClientInputMsg { ActionType = ActionTypes.Move, TargetX = 1, TargetY = 0 },
                    _ => null
                };
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

        if (msg != null && _ctx.Connection != null)
        {
            msg.Tick = _ctx.GameState.WorldTick;
            _ = _ctx.Connection.SendInputAsync(msg);
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

        renderer.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);
        _worldRenderer.Render(renderer, _ctx.GameState, gameCols, totalRows, shakeX, shakeY);
        _hudRenderer.Render(renderer, _ctx.GameState, gameCols, totalRows);

        // Render particles
        int halfW = gameCols / 2;
        int halfH = totalRows / 2;
        _ctx.Particles.Render(renderer, _ctx.GameState.PlayerX, _ctx.GameState.PlayerY,
            halfW, halfH, shakeX, shakeY);

        _overlayRenderer.RenderChat(renderer, totalCols, totalRows, _ctx.Chat);
        _overlayRenderer.RenderPerformance(renderer, _ctx.Performance);
    }
}
