using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PoeCurrencySpammer.Models;
using PoeCurrencySpammer.Services;
using PoeCurrencySpammer.ViewModels;
using PoeCurrencySpammer.Views;

namespace PoeCurrencySpammer;

public partial class App : Application
{
    private GlobalHotkeyService? _hotkeyService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = AppConfig.Load();

        var services = new ServiceCollection();
        services.AddSingleton(config);
        services.AddSingleton<InputSimulatorService>();
        services.AddSingleton<ClipboardService>();
        services.AddSingleton<GlobalHotkeyService>();
        services.AddSingleton<SoundService>();
        services.AddSingleton<ItemParserService>();
        services.AddSingleton<StatsLoaderService>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SettingsViewModel>(sp =>
            new SettingsViewModel(config, msg => { }, sp.GetRequiredService<UpdateService>()));
        services.AddSingleton<MainWindow>();

        var provider = services.BuildServiceProvider();

        _hotkeyService = provider.GetRequiredService<GlobalHotkeyService>();
        _hotkeyService.Install();

        var mainWindow = provider.GetRequiredService<MainWindow>();
        mainWindow.Show();
        MainWindow = mainWindow;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        base.OnExit(e);
    }
}
