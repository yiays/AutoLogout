using System;
using System.Threading.Tasks;

namespace AutoLogout;

public enum MessageBoxType { Error, Alert };
public enum SessionSwitchType { Lock, Unlock, Unknown };
public class SessionSwitchEventArgs
{
    public SessionSwitchType Type;
}

public interface IOS
{
    public event EventHandler<SessionSwitchEventArgs>? SessionSwitch;
    public void Initialize();
    public void Notify(string header, string content);
    public Task<SyncedState> LoadState();
    public Task SaveState(SyncedState state);
    public Task ClearState();
    public void Mute();
    public void UnMute();
    public void Relaunch(string args);
    public void RelaunchAsAdmin(string args);
    public void RegisterStartup(bool enable);
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
#else
        return new OSUnix();
#endif
    }
}