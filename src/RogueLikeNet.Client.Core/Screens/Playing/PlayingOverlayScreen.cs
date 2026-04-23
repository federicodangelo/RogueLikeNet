using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.Rendering.Overlays;

namespace RogueLikeNet.Client.Core.Screens.Playing;

/// <summary>
/// Base class for screens that render the game world (with particles, chat, perf, and quest tracker overlays)
/// underneath a HUD panel. Concrete subclasses (Inventory, Crafting, Quest Log, NPC Dialogue) implement
/// <see cref="RenderPanel"/> to draw their specific panel contents; the backdrop and standard overlays
/// are handled here via a sealed template-method <see cref="Render"/>.
/// </summary>
public abstract class PlayingOverlayScreen : IScreen
{
    protected readonly ScreenContext _ctx;
    private readonly PlayingBackdropRenderer _backdrop;
    private readonly OverlayRenderer _overlayRenderer;

    protected PlayingOverlayScreen(ScreenContext ctx, PlayingBackdropRenderer backdrop, OverlayRenderer overlayRenderer)
    {
        _ctx = ctx;
        _backdrop = backdrop;
        _overlayRenderer = overlayRenderer;
    }

    public abstract ScreenState ScreenState { get; }
    public abstract void HandleInput(IInputManager input);

    /// <summary>
    /// Default update — advances particles and screen shake. Override and call <c>base.Update</c> if
    /// additional per-frame work is needed.
    /// </summary>
    public virtual void Update(float deltaTime)
    {
        _ctx.Particles.Update(deltaTime);
        _ctx.ScreenShake.Update(_ctx.GameState.PlayerState?.Health ?? 0);
    }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        int gameCols = _backdrop.RenderWorld(renderer, _ctx, totalCols, totalRows);
        RenderPanel(renderer, gameCols, totalRows);
        _backdrop.RenderParticles(renderer, _ctx, totalCols, totalRows);
        RenderStandardOverlays(renderer, totalCols, totalRows, gameCols);
    }

    /// <summary>Draw the screen's HUD panel starting at column <paramref name="hudStartCol"/>.</summary>
    protected abstract void RenderPanel(ISpriteRenderer renderer, int hudStartCol, int totalRows);

    /// <summary>Renders the standard overlays (chat, performance, quest tracker) respecting user options.</summary>
    protected void RenderStandardOverlays(ISpriteRenderer renderer, int totalCols, int totalRows, int gameCols)
    {
        _overlayRenderer.RenderChat(renderer, totalCols, totalRows, _ctx.Chat);
        if (_ctx.Options.ShowStats)
            _overlayRenderer.RenderPerformance(renderer, gameCols, _ctx.Performance, _ctx.Debug);
        if (_ctx.Options.ShowQuestTracker)
            _overlayRenderer.RenderQuestTracker(renderer, gameCols, totalRows, _ctx.GameState.PlayerState);
    }

    /// <summary>
    /// Handles the cross-navigation pattern shared by Inventory / Crafting / Quest Log:
    /// <list type="bullet">
    ///   <item><c>MenuBack</c> → return to Playing.</item>
    ///   <item>Pressing the toggle key for the active screen (e.g. OpenInventory while on Inventory) → Playing.</item>
    ///   <item>Pressing the toggle key for another screen → transition to that screen (after invoking <see cref="OnLeavingViaSharedNavigation"/>).</item>
    /// </list>
    /// Returns <c>true</c> if the input was consumed.
    /// </summary>
    protected bool TryHandleSharedNavigation(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _ctx.RequestTransition(ScreenState.Playing);
            return true;
        }

        var self = ScreenState;

        if (input.IsActionPressed(InputAction.OpenInventory))
        {
            if (self == ScreenState.Inventory)
            {
                _ctx.RequestTransition(ScreenState.Playing);
            }
            else
            {
                OnLeavingViaSharedNavigation(ScreenState.Inventory);
                _ctx.RequestTransition(ScreenState.Inventory);
            }
            return true;
        }

        if (input.IsActionPressed(InputAction.OpenCrafting))
        {
            if (self == ScreenState.Crafting)
            {
                _ctx.RequestTransition(ScreenState.Playing);
            }
            else
            {
                OnLeavingViaSharedNavigation(ScreenState.Crafting);
                _ctx.RequestTransition(ScreenState.Crafting);
            }
            return true;
        }

        if (input.IsActionPressed(InputAction.OpenQuestLog))
        {
            if (self == ScreenState.QuestLog)
            {
                _ctx.RequestTransition(ScreenState.Playing);
            }
            else
            {
                OnLeavingViaSharedNavigation(ScreenState.QuestLog);
                _ctx.RequestTransition(ScreenState.QuestLog);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Invoked by <see cref="TryHandleSharedNavigation"/> immediately before transitioning to another
    /// playing-overlay screen (Inventory / Crafting / Quest Log). Override to remember per-screen state
    /// (e.g. selection and scroll position) so it survives the round trip.
    /// </summary>
    protected virtual void OnLeavingViaSharedNavigation(ScreenState target) { }
}
