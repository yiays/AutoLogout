using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace AutoLogout;

public enum MessageBoxType { Error, Alert };
public enum SessionSwitchType { Lock, Unlock, Unknown };
public class SessionSwitchEventArgs
{
    public SessionSwitchType Type;
}
public class FocusedWindow
{
    public required string exeName;
    public required string windowName;
    public Bitmap? icon;
}

public interface IOS
{
    public event EventHandler<SessionSwitchEventArgs>? SessionSwitch;
    public void Initialize();
    public void Notify(string header, string content);
    public Task<SyncedState> LoadState();
    public Task SaveState();
    public Task ClearState();
    public FocusedWindow? GetFocused();
    public void Chime();
    public void Mute();
    public void UnMute();
    public void Relaunch(string args);
    public Task<bool> RelaunchAsAdmin(string args);
    public void RegisterStartup(bool enable);
    public bool AutoStart { get; }
    public void Logoff();
    public void Shutdown();
}

public static class OS
{
    public static readonly IOS Current = Create();

    private static IOS Create()
    {
#if WINDOWS
        return new OSWindows();
#elif MACOS
        return new OSMac();
#else
        return new OSUnix();
#endif
    }
}