using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Connecting screen — shows "Connecting..." or error message.
/// </summary>
public sealed class ConnectingScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly MenuRenderer _menuRenderer;

    private string? _connectionError;

    public ScreenState ScreenState => ScreenState.Connecting;

    public ConnectingScreen(ScreenContext ctx, MenuRenderer menuRenderer)
    {
        _ctx = ctx;
        _menuRenderer = menuRenderer;
    }

    public void SetError(string? error) => _connectionError = error;
    public void ClearError() => _connectionError = null;

    public void HandleInput(IInputManager input)
    {
        if (_connectionError != null && input.IsActionPressed(InputAction.MenuConfirm))
        {
            _ctx.RequestTransition(Rendering.ScreenState.MainMenu);
            _connectionError = null;
        }
    }

    public void Update(float deltaTime) { }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        _menuRenderer.RenderConnecting(renderer, totalCols, totalRows, _connectionError);
    }
}
