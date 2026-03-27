using System.Diagnostics;

namespace RogueLikeNet.Client.Core.Systems;

/// <summary>
/// Tracks FPS, network latency, and bandwidth metrics.
/// </summary>
public sealed class PerformanceMonitor
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private int _frameCount;

    public int Fps { get; private set; }
    public int LatencyMs { get; private set; }
    public double BandwidthInKBps { get; private set; }
    public double BandwidthOutKBps { get; private set; }

    private long _lastDeltaTicks;
    private long _lastBytesSent;
    private long _lastBytesReceived;

    public void RecordDelta()
    {
        long now = Stopwatch.GetTimestamp();
        if (_lastDeltaTicks > 0)
            LatencyMs = (int)((now - _lastDeltaTicks) * 1000 / Stopwatch.Frequency);
        _lastDeltaTicks = now;
    }

    public void Update(long bytesSent, long bytesReceived)
    {
        _frameCount++;

        if (_stopwatch.ElapsedMilliseconds >= 1000)
        {
            Fps = _frameCount;
            _frameCount = 0;

            double elapsed = _stopwatch.Elapsed.TotalSeconds;
            BandwidthOutKBps = (bytesSent - _lastBytesSent) / 1024.0 / elapsed;
            BandwidthInKBps = (bytesReceived - _lastBytesReceived) / 1024.0 / elapsed;
            _lastBytesSent = bytesSent;
            _lastBytesReceived = bytesReceived;

            _stopwatch.Restart();
        }
    }
}
