using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Win32;

namespace AutoLogout;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Get the current state of the registry
            //TODO: make this multiplatform
            bool LocalRegistry = false;
            #if WINDOWS
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(State.REGKEY))
            {
                string? rawGuid = (string?)key?.GetValue("guid", null);
                if (rawGuid is not null) LocalRegistry = true;
            }
            #endif
            bool GlobalRegistry = false;
            #if WINDOWS
            using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(
              @"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                string regValue = (string)(key?.GetValue("AutoLogout") ?? "");
                if (regValue.Contains(Common.exePath)) GlobalRegistry = true;
            }
            #endif

            // Handle special parameters
            if(desktop.Args is null)
                throw new Exception("Args cannot be null!");
            if (desktop.Args.Contains("--register"))
            {
                // Register AutoLogout to start automatically on login
                // This requires Admin privileges
                if (!Environment.IsPrivilegedProcess)
                {
                    Common.RelaunchAsAdmin("--register");
                    return;
                }
                Common.RegisterStartup(true);
                return;
            }
            if (desktop.Args.Contains("--unregister"))
            {
                // Unregister AutoLogout so it no longer starts automatically on login
                // This requires Admin privileges
                if (!Environment.IsPrivilegedProcess)
                {
                    Common.RelaunchAsAdmin("--unregister");
                    return;
                }
                Common.RegisterStartup(false);
                return;
            }
            if (desktop.Args.Contains("--service"))
            {
                // The --service tag indicates that AutoLogout launched automatically from any account
                // Refuse to run if AutoLogout is not configured for this account
                if (!LocalRegistry) return;
                // Continue normal startup
            }

            if (!LocalRegistry && !GlobalRegistry && !desktop.Args.Contains("--skipsetup"))
            {
                // Run first time setup
                desktop.MainWindow = new FirstTimeSetup();
            }
            else
            {
                // Normal startup
                desktop.MainWindow = new MainWindow();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Focus();
        }
    }
}