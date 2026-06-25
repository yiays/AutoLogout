using System;
using System.Diagnostics;
using Microsoft.Win32;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace AutoLogout;

internal sealed class OSWindows : IOS
{
#if DEBUG
    readonly string REGKEY = "Software\\Yiays\\AutoLogout-Preview";
#else
    readonly string REGKEY = "Software\\Yiays\\AutoLogout";
#endif
    string ExecutablePath
    {
      get
      {
        return Process.GetCurrentProcess().MainModule?.FileName
          ?? throw new Exception("Unable to get current executable name.");
      }
    }
    public void Initialize()
    {
        // Register the notification handler before calling Register
        AppNotificationManager.Default.NotificationInvoked += (sender, args) =>
        {
            // Handle notification activation.
            // args.Argument contains the arguments from the notification
            // or button that was clicked, as key=value pairs separated
            // by '&', for example "action=acknowledge".
            Console.WriteLine($"Notification activated! Arguments: {args.Argument}");
        };

        AppNotificationManager.Default.Register();
    }
    public void Notify(string header, string content)
    {
        var notif = new AppNotificationBuilder()
            .AddArgument("action", "ViewItem")
            .AddText(header)
            .AddText(content)
            .BuildNotification();
        AppNotificationManager.Default.Show(notif);
    }
    public void Relaunch(string args)
    {
        var startInfo = new ProcessStartInfo(ExecutablePath)
        {
            UseShellExecute = true,
            Arguments = args
        };
        Process.Start(startInfo);
    }
    public void RelaunchAsAdmin(string args)
    {
        var startInfo = new ProcessStartInfo(ExecutablePath)
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = args
        };
        try
        {
            Process.Start(startInfo);
        }
        catch
        {
            // User cancelled UAC
        }
    }
  
    public void RegisterStartup(bool enable)
    {
        /// <summary>
        /// Register this application to start on boot systemwide
        /// </summary>

        string appName = "AutoLogout";
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", true
        );
        if (key is null) throw new Exception("System startup registry doesn't exist!");
        if (enable)
        {
            key.SetValue(appName, $"\"{ExecutablePath}\" --service");
            Notify("AutoLogout Setup", "AutoLogout has been configured to start on login.");
        }
        else
        {
            key.DeleteValue(appName, false);
            Notify("AutoLogout Setup", "AutoLogout will no longer start on login.");
        }
    }
}