using System.ComponentModel;
using System.Windows;
using PoeCurrencySpammer.ViewModels;

namespace PoeCurrencySpammer.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _mainVm;

    public MainWindow(MainViewModel mainVm, SettingsViewModel settingsVm)
    {
        InitializeComponent();
        _mainVm = mainVm;

        // Wire up auto-scroll for console
        _mainVm.SetScrollAction(() =>
        {
            Dispatcher.BeginInvoke(() => ConsoleBox.ScrollToEnd());
        });

        // Set DataContext per tab via code — the Main tab binds to MainViewModel
        // and Settings tab binds to SettingsViewModel.
        // For simplicity, use a composite object.
        DataContext = new CompositeViewModel(mainVm, settingsVm);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _mainVm.StopCommand.Execute(null);
        base.OnClosing(e);
    }
}

/// <summary>
/// Combines both ViewModels so both tabs can bind from one DataContext.
/// Main tab properties are forwarded from MainViewModel.
/// </summary>
public class CompositeViewModel : INotifyPropertyChanged
{
    public MainViewModel Main { get; }
    public SettingsViewModel Settings { get; }

    public CompositeViewModel(MainViewModel main, SettingsViewModel settings)
    {
        Main = main;
        Settings = settings;
        main.PropertyChanged += (s, e) => PropertyChanged?.Invoke(this, e);
        settings.PropertyChanged += (s, e) => PropertyChanged?.Invoke(this, e);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
