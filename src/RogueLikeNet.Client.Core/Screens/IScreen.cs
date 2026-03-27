using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Interface for a self-contained game screen that handles its own input, update, and rendering.
/// </summary>
public interface IScreen
{
    ScreenState ScreenState { get; }
    void HandleInput(IInputManager input);
    void Update(float deltaTime);
    void Render(ISpriteRenderer renderer, int totalCols, int totalRows);
}
