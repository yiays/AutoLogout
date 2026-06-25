namespace AutoLogout;

public interface IOS
{
  public void Initialize();
  public void Notify(string header, string content);
  public void Relaunch(string args);
  public void RelaunchAsAdmin(string args);
  public void RegisterStartup(bool enable);
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