using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using NAudio.CoreAudioApi;

namespace AutoLogout;

internal sealed class OSWindows : IOS
{
    public event EventHandler<SessionSwitchEventArgs>? SessionSwitch;
    private MMDeviceEnumerator? audioDeviceEnumerator;
    private MMDevice? audioDefaultDevice;
    private bool? audioPreviousState = null;
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
        // Initialize the CoreAudio components
        audioDeviceEnumerator = new MMDeviceEnumerator();

        // Register system notifications
        if (AppNotificationManager.IsSupported())
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

        // Handle session switch events
        SystemEvents.SessionSwitch += (o, e) => {
            SessionSwitchType switchType;
            if (e.Reason == SessionSwitchReason.SessionLock)
                switchType = SessionSwitchType.Lock;
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
                switchType = SessionSwitchType.Unlock;
            else
                switchType = SessionSwitchType.Unknown;
            var eventArgs = new SessionSwitchEventArgs { Type = switchType };
            SessionSwitch?.Invoke(this, eventArgs);
        };
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
    public async Task<SyncedState> LoadState()
    {
        RegistryKey key = Registry.CurrentUser.CreateSubKey(REGKEY, true) ??
            throw new Exception("Unable to load settings.");

        string? rawAuthKey = (string?)key.GetValue("authKey", null);
        string? rawGuid = (string?)key.GetValue("guid", null);
        string bedtimeRaw = (string)key.GetValue("bedtime", "0:00");
        string waketimeRaw = (string)key.GetValue("waketime", "0:00");

        return new SyncedState
        {
            Online = bool.Parse((string)key.GetValue("OnlineMode", "false")),
            authKey = rawAuthKey is null ? Guid.Empty : new Guid(rawAuthKey),
            uuid = rawGuid is null ? Guid.Empty : new Guid(rawGuid),
            hashedPassword = (string)key.GetValue("password", ""),
            bedtime = TimeOnly.Parse(bedtimeRaw),
            waketime = TimeOnly.Parse(waketimeRaw),
            dailyTimeLimit = (int)key.GetValue("dailyTimeLimit", -1),
            usageDate = DateOnly.Parse((string)key.GetValue("usageDate", "1/01/0001")),
            todayTimeLimit = (int)key.GetValue("todayTimeLimit", -1),
            usedTime = (int)key.GetValue("usedTime", 0)
        };
    }
    public async Task SaveState(SyncedState state)
    {
        RegistryKey key = Registry.CurrentUser.CreateSubKey(REGKEY, true) ??
            throw new Exception("Unable to save settings.");

        key.SetValue("OnlineMode", state.Online);
        key.SetValue("authKey", state.authKey);
        key.SetValue("guid", state.uuid);
        key.SetValue("password", state.hashedPassword);
        key.SetValue("usageDate", DateOnly.FromDateTime(DateTime.Today));
        key.SetValue("dailyTimeLimit", state.dailyTimeLimit);
        key.SetValue("todayTimeLimit", state.todayTimeLimit);
        key.SetValue("usedTime", state.usedTime);
        key.SetValue("bedtime", state.bedtime);
        key.SetValue("waketime", state.waketime);
    }
    public async Task ClearState()
    {
        Registry.CurrentUser.DeleteSubKeyTree(REGKEY);
    }
    public void Mute()
    {
        if (audioDeviceEnumerator is null)
        {
            Console.WriteLine("audioDeviceEnumerator hasn't been initialized yet!");
            return;
        }
        try
        {
            audioDefaultDevice = audioDeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch
        {
            Console.WriteLine("Failed to get default audio output device.");
            return;
        }
        if (audioPreviousState is null)
            audioPreviousState = audioDefaultDevice.AudioEndpointVolume.Mute;
        foreach(var device in audioDeviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)) {
            device.AudioEndpointVolume.Mute = true;
        }
    }
    public void UnMute()
    {
        if(audioPreviousState == false && audioDefaultDevice is not null && audioDeviceEnumerator is not null)
        {
            foreach (var dev in audioDeviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                dev.AudioEndpointVolume.Mute = false;
            }
        }
        audioPreviousState = null;
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
    public bool AutoStart
    {
        get
        {
            string appName = "AutoLogout";
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true
            );
            return (bool?)key?.GetValue(appName) ?? false;
        }
    }

    public void Logoff()
    {
#if DEBUG
        Console.Write("Log out called");
#else
        Process.Start("shutdown", "/l /f");
#endif
    }
    public void Shutdown()
    {
#if DEBUG
        Console.Write("Shut down called");
#else
        Process.Start("shutdown", "/p /f");
#endif
    }
}