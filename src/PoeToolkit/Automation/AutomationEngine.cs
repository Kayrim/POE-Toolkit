using System.Diagnostics;
using System.Drawing;
using System.IO;
using PoeCurrencySpammer.Automation.Strategies;
using PoeCurrencySpammer.Models;
using PoeCurrencySpammer.Services;
using static PoeCurrencySpammer.Services.NativeMethods;

namespace PoeCurrencySpammer.Automation;

public class AutomationEngine
{
    private readonly InputSimulatorService _input;
    private readonly GlobalHotkeyService _hotkeys;
    private readonly SoundService _sound;
    private readonly AppConfig _config;

    private CancellationTokenSource? _cts;
    private readonly IProgress<string> _log;

    public bool IsRunning => _cts is not null && !_cts.IsCancellationRequested;

    public AutomationEngine(InputSimulatorService input, GlobalHotkeyService hotkeys,
        SoundService sound, AppConfig config, IProgress<string> log)
    {
        _input = input;
        _hotkeys = hotkeys;
        _sound = sound;
        _config = config;
        _log = log;
    }

    /// <summary>Event raised when the engine stops itself (ESC pressed, etc.)</summary>
    public event Action? Stopped;

    /// <summary>Event raised when status phase changes (e.g., "Setup", "Running")</summary>
    public event Action<string>? StatusChanged;

    public void Start(ICurrencyStrategy strategy)
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        // Register ESC key to cancel immediately during any phase
        _hotkeys.RegisterKey(VK_ESCAPE, OnEscPressed);

        Task.Run(() => RunLoop(strategy, ct), ct);
    }

    public void Stop()
    {
        _hotkeys.UnregisterAll();
        _input.ReleaseShift();
        _cts?.Cancel();
        _cts = null;
    }

    private void OnEscPressed()
    {
        _log.Report("\nESC pressed - stopping...");
        _input.ReleaseShift();
        _cts?.Cancel();
        Stopped?.Invoke();
    }

    private async Task RunLoop(ICurrencyStrategy strategy, CancellationToken ct)
    {
        _log.Report($"=== PoE Currency Spammer ===");
        _log.Report($"Mode: {strategy.ModeName}");
        _log.Report($"Press F5 at each prompt. Press ESC to stop.\n");
        StatusChanged?.Invoke($"Setup ({strategy.ModeName})");

        int sessionCount = 0;
        int totalCurrency = 0;

        while (!ct.IsCancellationRequested)
        {
            sessionCount++;
            _log.Report($"--- Session #{sessionCount} ---");

            // Setup coordinates
            Point[]? coords;
            try
            {
                coords = await SetupCoordinates(strategy, ct);
            }
            catch (OperationCanceledException) { break; }

            if (coords is null) break;

            _log.Report("Coordinates saved! Spamming...");
            StatusChanged?.Invoke($"Running ({strategy.ModeName})");

            int currencyCount = 0;
            int errorCount = 0;
            var sw = Stopwatch.StartNew();

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    MatchResult result;
                    try
                    {
                        result = strategy.ExecuteIteration(coords, ct);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _log.Report($"Error: {ex.Message}");
                        continue;
                    }

                    currencyCount++;

                    if (result.IsMatch)
                    {
                        // Save last item
                        try
                        {
                            var path = Path.Combine(AppContext.BaseDirectory, "last_item.txt");
                            File.WriteAllText(path, result.Text);
                        }
                        catch { }

                        _log.Report($"\nDesired mod found after {currencyCount} currency used!");
                        _log.Report($"Matched: {result.Text}");

                        _sound.PlayMatchAlert(_config.MatchFoundFrequency, _config.MatchFoundDuration);

                        // Wait for resume or exit
                        var action = await WaitForResumeOrExit(ct);
                        if (action == "exit") goto done;
                        if (action == "reset") break; // restart session
                    }

                    if (currencyCount % 25 == 0)
                    {
                        double rate = sw.Elapsed.TotalSeconds > 0 ? currencyCount / sw.Elapsed.TotalSeconds : 0;
                        _log.Report($"Used {currencyCount} currency so far... ({rate:F1}/sec, {errorCount} errors)");
                    }
                }
            }
            catch (OperationCanceledException) { }

            sw.Stop();
            totalCurrency += currencyCount;
            if (currencyCount > 0 && !ct.IsCancellationRequested)
            {
                double finalRate = sw.Elapsed.TotalSeconds > 0 ? currencyCount / sw.Elapsed.TotalSeconds : 0;
                _log.Report($"\nSession #{sessionCount} finished. Currency used: {currencyCount}");
                _log.Report($"Time: {sw.Elapsed.TotalSeconds:F1}s | Rate: {finalRate:F1}/sec");
            }
        }

        done:
        _hotkeys.UnregisterAll();
        _input.ReleaseShift();
        if (!ct.IsCancellationRequested)
            _log.Report("\nScript finished.");
        Stopped?.Invoke();
    }

    private async Task<Point[]?> SetupCoordinates(ICurrencyStrategy strategy, CancellationToken ct)
    {
        var labels = strategy.CoordinateLabels;
        var coords = new Point[labels.Length];

        for (int i = 0; i < labels.Length; i++)
        {
            int step = i + 1;
            string stepLabel = labels.Length == 1
                ? "Final"
                : i == labels.Length - 1 ? $"Step {step} (Final)" : $"Step {step}";

            _log.Report($"{stepLabel}: Position mouse over {labels[i]} and press F5...");

            await _hotkeys.WaitForKeyAsync(VK_F5, ct);
            // Wait for key release
            while (GlobalHotkeyService.IsKeyPressed(VK_F5) && !ct.IsCancellationRequested)
                await Task.Delay(50, ct);

            coords[i] = _input.GetCursorPosition();
            _log.Report($"  {labels[i]} position saved: ({coords[i].X}, {coords[i].Y})");

            // If this is the item (last coord), move mouse away for tooltip
            if (i == labels.Length - 1)
            {
                _input.MoveTo(coords[i].X + 50, coords[i].Y + 50, 0.05);
            }
        }

        return coords;
    }

    private async Task<string> WaitForResumeOrExit(CancellationToken ct)
    {
        _log.Report($"\nMatch found! Press F5 to START NEW ITEM or ESC to exit.");

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = ct.Register(() => tcs.TrySetResult("exit"));

        void OnF5() => tcs.TrySetResult("reset");
        void OnEsc() => tcs.TrySetResult("exit");

        _hotkeys.RegisterKey(VK_F5, OnF5);
        _hotkeys.RegisterKey(VK_ESCAPE, OnEsc);

        try
        {
            var result = await tcs.Task;
            // Wait for key release
            await Task.Delay(100, CancellationToken.None);
            if (result == "reset")
                _log.Report("Resetting to start new item...");
            return result;
        }
        finally
        {
            // Cleanup handlers
            if (_hotkeys is not null)
            {
                // We rely on the handlers being removed in the next setup cycle
                // or engine stop. Acceptable for this use case.
            }
        }
    }
}
