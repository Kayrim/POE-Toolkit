using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoeCurrencySpammer.Automation;
using PoeCurrencySpammer.Automation.Strategies;
using PoeCurrencySpammer.Models;
using PoeCurrencySpammer.Services;

namespace PoeCurrencySpammer.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly InputSimulatorService _input;
    private readonly ClipboardService _clipboard;
    private readonly ItemParserService _parser;
    private readonly GlobalHotkeyService _hotkeys;
    private readonly SoundService _sound;
    private readonly StatsLoaderService _statsLoader;
    private AutomationEngine? _engine;

    private readonly StringBuilder _consoleBuffer = new();
    private Action? _scrollToEnd;

    [ObservableProperty] private string _searchRegex = "dic|cil|flar";
    [ObservableProperty] private string _modSearchText = "";
    [ObservableProperty] private StatEntry? _selectedSearchResult;

    public ObservableCollection<StatEntry> ModSearchResults { get; } = [];
    public ObservableCollection<StatEntry> SelectedMods { get; } = [];
    [ObservableProperty] private string _minLinksText = "0";
    [ObservableProperty] private string _chromaticR = "0";
    [ObservableProperty] private string _chromaticG = "0";
    [ObservableProperty] private string _chromaticB = "0";
    // Alteration + Augmentation mode
    [ObservableProperty] private string _altAugMode = "None";
    public string[] AltAugModeOptions { get; } = ["None", "Prefix", "Suffix"];

    [ObservableProperty] private string _consoleText = "";
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private Brush _statusBrush = new SolidColorBrush(Color.FromRgb(0x0e, 0xad, 0x69));
    [ObservableProperty] private bool _isRunning;
    private string? _currentModeLabel;

    // Autoclicker
    [ObservableProperty] private bool _autoclickerCtrl;
    [ObservableProperty] private bool _autoclickerShift;
    [ObservableProperty] private string _autoclickerIntervalMs = "50";
    private CancellationTokenSource? _autoclickerCts;

    public MainViewModel(AppConfig config, InputSimulatorService input, ClipboardService clipboard,
        ItemParserService parser, GlobalHotkeyService hotkeys, SoundService sound, StatsLoaderService statsLoader)
    {
        _config = config;
        _input = input;
        _clipboard = clipboard;
        _parser = parser;
        _hotkeys = hotkeys;
        _sound = sound;
        _statsLoader = statsLoader;

        SearchRegex = config.SearchRegex;

        // Pre-warm stats in background
        Task.Run(() => _ = _statsLoader.AllStats);
    }

    partial void OnModSearchTextChanged(string value)
    {
        ModSearchResults.Clear();
        if (string.IsNullOrWhiteSpace(value) || value.Length < 2) return;

        foreach (var stat in _statsLoader.Search(value, 30))
            ModSearchResults.Add(stat);
    }

    partial void OnSelectedSearchResultChanged(StatEntry? value)
    {
        if (value is null) return;
        AddMod(value);
        SelectedSearchResult = null;
        ModSearchText = "";
    }

    [RelayCommand]
    private void AddMod(StatEntry mod)
    {
        if (SelectedMods.Any(m => m.Id == mod.Id)) return;
        SelectedMods.Add(mod);
        UpdateRegexFromMods();
    }

    [RelayCommand]
    private void RemoveMod(StatEntry mod)
    {
        SelectedMods.Remove(mod);
        UpdateRegexFromMods();
    }

    [RelayCommand]
    private void ClearMods()
    {
        SelectedMods.Clear();
        SearchRegex = "";
    }

    private void UpdateRegexFromMods()
    {
        SearchRegex = StatsLoaderService.ModsToRegex(SelectedMods);
    }

    public void SetScrollAction(Action scrollToEnd) => _scrollToEnd = scrollToEnd;

    private IProgress<string> CreateLogger()
    {
        return new Progress<string>(msg =>
        {
            _consoleBuffer.AppendLine(msg);
            ConsoleText = _consoleBuffer.ToString();
            _scrollToEnd?.Invoke();
        });
    }

    private void StartMode(ICurrencyStrategy strategy, string label)
    {
        if (IsRunning)
        {
            // Same mode already running — do nothing
            if (_currentModeLabel == label) return;
            // Different mode — stop current first
            Stop();
        }

        SyncConfig();
        _consoleBuffer.Clear();
        ConsoleText = "";

        var log = CreateLogger();
        _engine = new AutomationEngine(_input, _hotkeys, _sound, _config, log);
        _engine.Stopped += () =>
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                _engine = null;
                IsRunning = false;
                _currentModeLabel = null;
                StatusText = "Ready";
                StatusBrush = new SolidColorBrush(Color.FromRgb(0x0e, 0xad, 0x69));
            });
        };
        _engine.StatusChanged += (status) =>
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                StatusText = status;
            });
        };

        IsRunning = true;
        _currentModeLabel = label;
        StatusText = $"Setup ({label}) - Press F5 at prompts";
        StatusBrush = new SolidColorBrush(Color.FromRgb(0xf7, 0x7f, 0x00)); // orange

        _engine.Start(strategy);
    }

    [RelayCommand]
    private void StartAlteration()
    {
        var strategy = new AlterationStrategy(_input, _clipboard, _parser, _config);
        StartMode(strategy, "Alteration");
    }

    [RelayCommand]
    private void StartAlchScour()
    {
        var strategy = new AlchScourStrategy(_input, _clipboard, _parser, _config);
        StartMode(strategy, "Alch/Scour");
    }

    [RelayCommand]
    private void StartLinks()
    {
        var strategy = new LinksStrategy(_input, _clipboard, _parser, _config);
        StartMode(strategy, "Links");
    }

    [RelayCommand]
    private void StartChromatic()
    {
        var strategy = new ChromaticStrategy(_input, _clipboard, _parser, _config);
        StartMode(strategy, "Chromatic");
    }

    [RelayCommand]
    private void StartAutoclicker()
    {
        if (_autoclickerCts is not null)
            return;

        // Stop any currency mode that might be running
        if (IsRunning)
            Stop();

        _consoleBuffer.Clear();
        ConsoleText = "";

        _ = int.TryParse(AutoclickerIntervalMs, out int interval);
        interval = Math.Max(10, interval);
        _config.AutoclickerIntervalMs = interval;

        bool holdCtrl = AutoclickerCtrl;
        bool holdShift = AutoclickerShift;

        var log = CreateLogger();
        log.Report("=== Autoclicker ===");
        log.Report($"Interval: {interval}ms | Ctrl: {holdCtrl} | Shift: {holdShift}");
        log.Report("Press F5 to start clicking. Press ESC to stop.\n");

        IsRunning = true;
        _currentModeLabel = "Autoclicker";
        StatusText = "Autoclicker - Press F5 to start";
        StatusBrush = new SolidColorBrush(Color.FromRgb(0xf7, 0x7f, 0x00)); // orange

        _autoclickerCts = new CancellationTokenSource();
        var ct = _autoclickerCts.Token;

        _hotkeys.RegisterKey(NativeMethods.VK_ESCAPE, StopAutoclicker);

        Task.Run(async () =>
        {
            try
            {
                await _hotkeys.WaitForKeyAsync(NativeMethods.VK_F5, ct);
                // Wait for F5 release
                while (GlobalHotkeyService.IsKeyPressed(NativeMethods.VK_F5) && !ct.IsCancellationRequested)
                    await Task.Delay(50, ct);

                log.Report("Autoclicking started!");
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    StatusText = "Autoclicker Running";
                    StatusBrush = new SolidColorBrush(Color.FromRgb(0x0e, 0xad, 0x69));
                });

                // Hold modifier keys
                if (holdCtrl) _input.KeyDown(NativeMethods.VK_CONTROL);
                if (holdShift) _input.KeyDown(NativeMethods.VK_SHIFT);

                int clicks = 0;
                var sw = System.Diagnostics.Stopwatch.StartNew();

                while (!ct.IsCancellationRequested)
                {
                    _input.Click("left", 0.02);
                    clicks++;

                    if (clicks % 100 == 0)
                    {
                        double rate = sw.Elapsed.TotalSeconds > 0 ? clicks / sw.Elapsed.TotalSeconds : 0;
                        log.Report($"Clicks: {clicks} ({rate:F1}/sec)");
                    }

                    await Task.Delay(interval, ct);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                // Release modifier keys
                if (holdCtrl) _input.KeyUp(NativeMethods.VK_CONTROL);
                if (holdShift) _input.KeyUp(NativeMethods.VK_SHIFT);

                log.Report("\nAutoclicker stopped.");
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    _autoclickerCts = null;
                    IsRunning = false;
                    _currentModeLabel = null;
                    StatusText = "Ready";
                    StatusBrush = new SolidColorBrush(Color.FromRgb(0x0e, 0xad, 0x69));
                });
            }
        }, ct);
    }

    private void StopAutoclicker()
    {
        _hotkeys.UnregisterAll();
        _autoclickerCts?.Cancel();
    }

    [RelayCommand]
    private void Stop()
    {
        if (_autoclickerCts is not null)
        {
            StopAutoclicker();
            return;
        }

        _engine?.Stop();
        _engine = null;
        IsRunning = false;
        _currentModeLabel = null;
        StatusText = "Ready";
        StatusBrush = new SolidColorBrush(Color.FromRgb(0x0e, 0xad, 0x69)); // green
    }

    [RelayCommand]
    private void TestRegex()
    {
        SyncConfig();
        var log = CreateLogger();
        log.Report("=== Test Regex Mode ===");
        log.Report($"Pattern: {_config.SearchRegex}");
        log.Report("Hover over an item and press F5 to test...");

        Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource();
                await _hotkeys.WaitForKeyAsync(NativeMethods.VK_F5, cts.Token);
                while (GlobalHotkeyService.IsKeyPressed(NativeMethods.VK_F5))
                    await Task.Delay(50);

                var itemText = _clipboard.CopyItemText(cts.Token);
                if (string.IsNullOrEmpty(itemText))
                {
                    log.Report("[No item data captured]");
                    return;
                }

                log.Report($"\n--- Item Text ---\n{itemText}");
                var result = _parser.CheckModQuality(itemText, _config.SearchRegex, debug: true);
                log.Report($"\nMatch: {(result.IsMatch ? "YES" : "NO")}");
                if (result.IsMatch)
                    log.Report($"Matched: {result.Text}");
            }
            catch (Exception ex)
            {
                log.Report($"Error: {ex.Message}");
            }
        });
    }

    private void SyncConfig()
    {
        _config.SearchRegex = SearchRegex;
        _config.AltAugMode = AltAugMode;
        _ = int.TryParse(MinLinksText, out int ml);
        _config.MinLinks = Math.Max(0, ml);
        _ = int.TryParse(ChromaticR, out int r);
        _config.ChromaticR = r;
        _ = int.TryParse(ChromaticG, out int g);
        _config.ChromaticG = g;
        _ = int.TryParse(ChromaticB, out int b);
        _config.ChromaticB = b;
    }
}
