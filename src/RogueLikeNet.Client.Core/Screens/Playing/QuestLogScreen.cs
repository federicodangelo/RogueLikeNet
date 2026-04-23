using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.Rendering.Hud;
using RogueLikeNet.Client.Core.Rendering.Overlays;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Screens.Playing;

/// <summary>
/// Quest journal — lists active quests and their objective progress.
/// Opened via the OpenQuestLog action (Q key) and closed with Esc/Q.
/// Allows abandoning a quest with X.
/// </summary>
public sealed class QuestLogScreen : PlayingOverlayScreen
{
    private readonly QuestLogRenderer _questLogRenderer;
    private int _selectedIndex;

    public override ScreenState ScreenState => ScreenState.QuestLog;

    public QuestLogScreen(ScreenContext ctx, PlayingBackdropRenderer backdrop,
        QuestLogRenderer questLogRenderer, OverlayRenderer overlayRenderer)
        : base(ctx, backdrop, overlayRenderer)
    {
        _questLogRenderer = questLogRenderer;
    }

    public void OnEnter()
    {
        _selectedIndex = 0;
    }

    public override void HandleInput(IInputManager input)
    {
        var quests = _ctx.GameState.PlayerState?.Quests?.Active ?? [];

        if (TryHandleSharedNavigation(input)) return;

        if (quests.Length == 0) return;

        if (InputHelpers.HandleListNavigation(input, ref _selectedIndex, quests.Length))
            return;

        if (input.IsActionPressed(InputAction.Drop))
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

    protected override void RenderPanel(ISpriteRenderer renderer, int hudStartCol, int totalRows)
    {
        var quests = _ctx.GameState.PlayerState?.Quests?.Active ?? [];
        _questLogRenderer.Render(renderer, quests, _selectedIndex, hudStartCol, totalRows);
    }
}
