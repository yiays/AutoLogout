using Avalonia;
using System;

namespace AutoLogout;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        OS.Current.Initialize();

        // Handle special parameters
        if (args.Contains("--register"))
        {
            // Register AutoLogout to start automatically on login
            // This requires an Admin privileges
            if (!Environment.IsPrivilegedProcess)
            {
                Console.WriteLine("--register was called without admin privileges");
                return;
            }
            OS.Current.RegisterStartup(true);
            return;
        }
        if (args.Contains("--unregister"))
        {
            // Unregister AutoLogout so it no longer starts automatically on login
            // This requires an Admin privileges
            if (!Environment.IsPrivilegedProcess)
            {
                Console.WriteLine("--unregister was called without admin privileges");
                return;
            }
            OS.Current.RegisterStartup(false);
            return;
        }
        if (args.Contains("--service"))
        {
            // The --service tag indicates that AutoLogout launched automatically from any account
            //TODO: Refuse to run if AutoLogout is not configured for this account
        }
        
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
