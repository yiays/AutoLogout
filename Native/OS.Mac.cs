using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AutoLogout;

internal sealed class OSMac : IOS
{
    public event EventHandler<SessionSwitchEventArgs>? SessionSwitch;
    public void Initialize()
    {
        //TODO
    }
    public void Notify(string header, string content)
    {
        //TODO
    }
    public Task<StoredState> LoadState()
    {
        //TODO
        throw new NotImplementedException();
    }
    public Task SaveState()
    {
        //TODO
        throw new NotImplementedException();
    }
    public Task ClearState()
    {
        //TODO
        throw new NotImplementedException();
    }
    public FocusedWindow? GetFocused()
    {
        return null;
    }
    // Native interop for macOS
    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    private static extern void NSBeep();
    public void Chime()
    {
        try
        {
            NSBeep();
            return;
        }
        catch
        {
            Console.WriteLine("Failed to invoke MacOS Chime");
        }
        Console.Beep();
    }
    public void Mute()
    {
        //TODO
    }
    public void UnMute()
    {
        //TODO
    }
    public void Relaunch(string args)
    {
        //TODO
    }
    public async Task<bool> RelaunchAsAdmin(string args)
    {
        //TODO
        return await Task.Run(() => false);
    }
    public void RegisterStartup(bool enable)
    {
        //TODO
    }
    public bool AutoStart { get => false; }
    public void Logoff()
    {
        //TODO
    }
    public void Shutdown()
    {
        //TODO
    }
}