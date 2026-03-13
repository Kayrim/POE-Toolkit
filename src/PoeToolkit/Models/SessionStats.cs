using System.Diagnostics;

namespace PoeCurrencySpammer.Models;

public class SessionStats
{
    private readonly Stopwatch _stopwatch = new();

    public int CurrencyCount { get; private set; }
    public int ErrorCount { get; private set; }
    public TimeSpan Elapsed => _stopwatch.Elapsed;
    public double Rate => Elapsed.TotalSeconds > 0 ? CurrencyCount / Elapsed.TotalSeconds : 0;

    public void Start()
    {
        CurrencyCount = 0;
        ErrorCount = 0;
        _stopwatch.Restart();
    }

    public void Stop() => _stopwatch.Stop();
    public void IncrementCurrency() => CurrencyCount++;
    public void IncrementError() => ErrorCount++;
}
