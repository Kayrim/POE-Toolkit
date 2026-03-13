using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoeCurrencySpammer.Models;

namespace PoeCurrencySpammer.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly Action<string> _log;

    [ObservableProperty] private string _delayAfterClick;
    [ObservableProperty] private string _matchFrequency;
    [ObservableProperty] private string _matchDuration;
    [ObservableProperty] private string _stopKey;
    [ObservableProperty] private string _startKey;
    [ObservableProperty] private string _saveMessage = "";

    public SettingsViewModel(AppConfig config, Action<string> log)
    {
        _config = config;
        _log = log;
        _delayAfterClick = config.DelayAfterClick.ToString();
        _matchFrequency = config.MatchFoundFrequency.ToString();
        _matchDuration = config.MatchFoundDuration.ToString();
        _stopKey = config.StopKey;
        _startKey = config.StartKey;
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
}
