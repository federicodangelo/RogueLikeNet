using Engine.Platform;
using RogueLikeNet.Client.Core.Networking;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Client.Core.Systems;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Shared context passed to all screens. Provides access to game state, systems, and transition callbacks.
/// </summary>
public sealed class ScreenContext
{
    public required ClientGameState GameState { get; init; }
    public required ParticleSystem Particles { get; init; }
    public required ChatSystem Chat { get; init; }
    public required PerformanceMonitor Performance { get; init; }
    public required ScreenShakeEffect ScreenShake { get; init; }

    public IGameServerConnection? Connection { get; set; }
    public ISpriteRenderer SpriteRenderer { get; set; } = null!;

    /// <summary>Request a screen transition.</summary>
    public required Action<ScreenState> RequestTransition { get; init; }

    // Game event callbacks — wired to RogueLikeGame public events
    public required Action<long, int, string> OnStartOffline { get; init; }
    public required Action<int, string> OnStartOnline { get; init; }
    public required Action OnReturnToMenu { get; init; }
    public required Action OnQuit { get; init; }
}
