using System.Diagnostics;

namespace RogueLikeNet.Client.Core.Systems;

/// <summary>
/// Manages screen shake triggered by player damage.
/// </summary>
public sealed class ScreenShakeEffect
{
    private int _lastKnownHealth;
    private long _shakeUntilTicks;
    private readonly Random _rng = new();

    public float OffsetX { get; private set; }
    public float OffsetY { get; private set; }

    public void Update(int currentHealth)
    {
        if (_lastKnownHealth > 0 && currentHealth < _lastKnownHealth)
            _shakeUntilTicks = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 4; // 250ms
        _lastKnownHealth = currentHealth;

        if (Stopwatch.GetTimestamp() < _shakeUntilTicks)
        {
            OffsetX = (_rng.NextSingle() - 0.5f) * 8f;
            OffsetY = (_rng.NextSingle() - 0.5f) * 8f;
        }
        else
        {
            OffsetX = 0;
            OffsetY = 0;
        }
    }

    public void Reset()
    {
        _lastKnownHealth = 0;
        _shakeUntilTicks = 0;
        OffsetX = 0;
        OffsetY = 0;
    }
}
