using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using NAudio.CoreAudioApi;
using System.Text.Json;
using System.Collections.Generic;

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
    public async Task<StoredState> LoadState()
    {
        RegistryKey key = Registry.CurrentUser.CreateSubKey(REGKEY, true) ??
            throw new Exception("Unable to load settings.");

        var rawAuthKey = (string?)key.GetValue("authKey", null);
        var rawGuid = (string?)key.GetValue("guid", null);
        var bedtimeRaw = (string)key.GetValue("bedtime", "0:00");
        var waketimeRaw = (string)key.GetValue("waketime", "0:00");
        var usageRaw = (string?)key.GetValue("usage", null);
        var appIconsRaw = (string?)key.GetValue("appIcons", null);

        return new StoredState
        {
            Online = bool.Parse((string)key.GetValue("OnlineMode", "false")),
            authKey = rawAuthKey is null ? Guid.Empty : new Guid(rawAuthKey),
            uuid = rawGuid is null ? Guid.Empty : new Guid(rawGuid),
            hashedPassword = (string)key.GetValue("password", ""),
            bedtime = TimeOnly.Parse(bedtimeRaw),
            waketime = TimeOnly.Parse(waketimeRaw),
            dailyTimeLimit = (int)key.GetValue("dailyTimeLimit", -1),
            usageDate = DateOnly.Parse((string)key.GetValue("usageDate", "1/01/0001")),
            usage = usageRaw is null? []: JsonSerializer.Deserialize<UsageRecord>(usageRaw) ?? [],
            appIcons = appIconsRaw is null? []: JsonSerializer.Deserialize<Dictionary<string,string>>(appIconsRaw) ?? [],
            todayTimeLimit = (int)key.GetValue("todayTimeLimit", -1),
            usedTime = (int)key.GetValue("usedTime", 0)
        };
    }
    public async Task SaveState()
    {
        RegistryKey key = Registry.CurrentUser.CreateSubKey(REGKEY, true) ??
            throw new Exception("Unable to save settings.");

        key.SetValue("OnlineMode", State.Current.Store.Online);
        key.SetValue("authKey", State.Current.Store.authKey);
        key.SetValue("guid", State.Current.Store.uuid);
        key.SetValue("password", State.Current.Store.hashedPassword);
        key.SetValue("usageDate", State.Current.Store.usageDate);
        key.SetValue("dailyTimeLimit", State.Current.Store.dailyTimeLimit);
        key.SetValue("todayTimeLimit", State.Current.Store.todayTimeLimit);
        key.SetValue("usedTime", State.Current.Store.usedTime);
        key.SetValue("usage", JsonSerializer.Serialize(State.Current.Store.usage));
        key.SetValue("appIcons", JsonSerializer.Serialize(State.Current.Store.appIcons));
        key.SetValue("bedtime", State.Current.Store.bedtime);
        key.SetValue("waketime", State.Current.Store.waketime);
    }
    public async Task ClearState()
    {
        Registry.CurrentUser.DeleteSubKeyTree(REGKEY);
    }
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    public FocusedWindow? GetFocused()
    {
        /// <summary>
        /// Gets the window currently in focus and returns a FocusedWindow object
        /// </summary>
        // Get the title of the current window
        IntPtr handle = GetForegroundWindow();
        StringBuilder title = new StringBuilder(256);
        if(GetWindowText(handle, title, 256) <= 0)
        {
            return null;
        }
        GetWindowThreadProcessId(handle, out uint pid);
        if (pid <= 0) {
            return null;
        }
        try {
            Process proc = Process.GetProcessById((int)pid);
            string? exePath = proc?.MainModule?.FileName;
            if(exePath is null) return null;
            var exeName = exePath.Split('\\').Last();
            if(!State.Current.IconRepo.ContainsKey(exeName))
            {
                System.Drawing.Icon? appIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if(appIcon is not null) {
                    using var win_bitmap = new System.Drawing.Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = System.Drawing.Graphics.FromImage(win_bitmap))
                    {
                        g.Clear(System.Drawing.Color.Transparent);
                        g.DrawIcon(appIcon, new System.Drawing.Rectangle(0, 0, 32, 32));
                    }
                    using var memoryStream = new MemoryStream();
                    win_bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                    _ = State.Current.AddIcon(exeName, memoryStream);
        }
            }
            return new FocusedWindow {
                exeName = exePath.Split('\\').Last(),
                windowName = title.ToString()
            };
        }
        catch (Exception ex) {
            Console.WriteLine("Error recording focused window: " + ex.Message);
            return null;
        }
    }
    public void Chime()
    {
        System.Media.SystemSounds.Exclamation.Play();
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
    public async Task<bool> RelaunchAsAdmin(string args)
    {
        return await Task.Run(() => {
            var startInfo = new ProcessStartInfo(ExecutablePath)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = args
            };
            try
            {
                Process.Start(startInfo);
                return true;
            }
            catch
            {
                // Likely user denied UAC
                return false;
            }
        });
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
                @"Software\Microsoft\Windows\CurrentVersion\Run", false
            );
            if(key?.GetValue(appName) is not null)
            {
                string value = (string?)key.GetValue(appName) ?? "";
                return value.Contains(ExecutablePath);
            }
            return false;
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