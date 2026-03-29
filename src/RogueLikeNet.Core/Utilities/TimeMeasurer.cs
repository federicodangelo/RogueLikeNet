namespace RogueLikeNet.Core.Utilities;

using System.Diagnostics;
using System.Runtime.CompilerServices;

public class TimeMeasurerAccumulator
{
    public static readonly ThreadLocal<TimeMeasurerAccumulator> ThreadInstance = new ThreadLocal<TimeMeasurerAccumulator>(() => new TimeMeasurerAccumulator());

    public record Measurement(string Name, TimeSpan Elapsed, int ParentIndex, int Depth, bool Hidden);

    private List<Measurement> _measurements = [];
    private List<Measurement> _lastCompletedMeasurements = [];
    private int _currentParentIndex = -1;
    private int _currentDepth = 0;
    private bool _currentHidden = false;

    public IReadOnlyList<Measurement> GetLastCompletedMeasurements() => _lastCompletedMeasurements;

    public void BeginMeasurement(string name, bool hidden)
    {
        _measurements.Add(new Measurement(name, TimeSpan.Zero, _currentParentIndex, _currentDepth, _currentHidden || hidden));
        _currentParentIndex = _measurements.Count - 1;
        _currentDepth++;
        _currentHidden = _currentHidden || hidden; // Once hidden, all children are also hidden
    }

    public void EndMeasurement(TimeSpan elapsed)
    {
        if (_currentParentIndex == -1)
            throw new InvalidOperationException("No active measurement to end.");

        var current = _measurements[_currentParentIndex];
        _measurements[_currentParentIndex] = current with { Elapsed = elapsed };
        _currentParentIndex = current.ParentIndex;
        _currentDepth--;
        if (_currentParentIndex == -1)
        {
            // Completed a top-level measurement, store current measurements and reset for next round
            (_lastCompletedMeasurements, _measurements) = (_measurements, _lastCompletedMeasurements);
            _measurements.Clear();
            _currentHidden = false;
        }
        else
        {
            _currentHidden = _measurements[_currentParentIndex].Hidden; // Restore hidden state of parent
        }
    }

}

public readonly struct TimeMeasurer : IDisposable
{
    private readonly long _startTimestamp;

    static public TimeMeasurer FromMethodName([CallerMemberName] string methodName = "")
    {
        return new TimeMeasurer(methodName);
    }

    public TimeMeasurer(string name, bool hidden = false)
    {
        _startTimestamp = Stopwatch.GetTimestamp();
        TimeMeasurerAccumulator.ThreadInstance.Value?.BeginMeasurement(name, hidden);
    }

    public TimeSpan ElapsedTime => Stopwatch.GetElapsedTime(_startTimestamp);

    public void Dispose()
    {
        TimeMeasurerAccumulator.ThreadInstance.Value?.EndMeasurement(ElapsedTime);
    }
}