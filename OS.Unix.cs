using System;
using System.Threading.Tasks;

namespace AutoLogout;

internal sealed class OSUnix : IOS
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
    public Task<SyncedState> LoadState()
    {
        //TODO
        throw new NotImplementedException();
    }
    public Task SaveState(SyncedState state)
    {
        //TODO
        throw new NotImplementedException();
    }
    public Task ClearState()
    {
        //TODO
        throw new NotImplementedException();
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
    public void RelaunchAsAdmin(string args)
    {
        //TODO
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