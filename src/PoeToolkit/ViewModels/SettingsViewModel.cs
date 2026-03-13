using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoeCurrencySpammer.Models;
using PoeCurrencySpammer.Services;

namespace PoeCurrencySpammer.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly Action<string> _log;
    private readonly UpdateService _update;

    [ObservableProperty] private string _delayAfterClick;
    [ObservableProperty] private string _matchFrequency;
    [ObservableProperty] private string _matchDuration;
    [ObservableProperty] private string _stopKey;
    [ObservableProperty] private string _startKey;
    [ObservableProperty] private string _saveMessage = "";

    // Update
    [ObservableProperty] private string _currentVersion = "";
    [ObservableProperty] private string _updateStatus = "";
    [ObservableProperty] private bool _updateAvailable;
    private string? _updateDownloadUrl;

    public SettingsViewModel(AppConfig config, Action<string> log, UpdateService update)
    {
        _config = config;
        _log = log;
        _update = update;
        _delayAfterClick = config.DelayAfterClick.ToString();
        _matchFrequency = config.MatchFoundFrequency.ToString();
        _matchDuration = config.MatchFoundDuration.ToString();
        _stopKey = config.StopKey;
        _startKey = config.StartKey;
        _currentVersion = update.CurrentVersion;
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            _config.DelayAfterClick = double.Parse(DelayAfterClick);
            _config.MatchFoundFrequency = int.Parse(MatchFrequency);
            _config.MatchFoundDuration = int.Parse(MatchDuration);
            _config.StopKey = StopKey;
            _config.StartKey = StartKey;
            _config.Save();
            SaveMessage = "Settings saved!";
            _log("Settings saved!");
        }
        catch (Exception ex)
        {
            SaveMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        try
        {
            UpdateStatus = "Checking...";
            UpdateAvailable = false;

            var result = await _update.CheckForUpdateAsync();
            if (result is null)
            {
                UpdateStatus = "You're up to date!";
            }
            else
            {
                _updateDownloadUrl = result.Value.DownloadUrl;
                UpdateStatus = $"Update available: {result.Value.Tag}";
                UpdateAvailable = true;
            }
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Check failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task InstallUpdate()
    {
        if (_updateDownloadUrl is null) return;

        try
        {
            var progress = new Progress<string>(msg => UpdateStatus = msg);
            await _update.DownloadAndApplyAsync(_updateDownloadUrl, progress);

            // Shut down the app — the updater script will replace the exe and relaunch
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown();
            });
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Update failed: {ex.Message}";
        }
    }
}
