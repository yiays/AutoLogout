using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;

namespace AutoLogout;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    public override void Initialize()
    {
        DataContext = this;
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();

            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        // Fetch the XAML-defined TrayIcon from the application instances
        var icons = TrayIcon.GetIcons(this);
        _trayIcon = icons?[0];
        // Change the icon whenever the theme changes
        this.GetObservable(ActualThemeVariantProperty)
            .Subscribe(new ThemeVariantObserver(UpdateTrayIconTheme));

        base.OnFrameworkInitializationCompleted();
    }
    public void ShowMainWindow(object? sender, EventArgs e)
    {
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Topmost = false;
            desktop.MainWindow?.Topmost = true;
        }
    }

    private void UpdateTrayIconTheme(ThemeVariant currentTheme)
    {
        if (_trayIcon == null) return;

        // Choose appropriate icon variant paths based on light/dark modes
        string resourcePath = currentTheme == ThemeVariant.Dark 
            ? "avares://AutoLogout/Resources/icon.ico"
            : "avares://AutoLogout/Resources/icon-light.ico";

        try
        {
            using var stream = AssetLoader.Open(new Uri(resourcePath));
            _trayIcon.Icon = new WindowIcon(stream);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update tray icon: {ex.Message}");
        }
    }
}