namespace AutoLogout;

using Microsoft.Win32;
using System;
using System.Reflection;

static class Program
{
  [STAThread]
  static void Main(string[] args)
  {
    // Ensure the base directory is correct
    Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

    // Help the runtime find library files that have been moved to .\Libraries\
    AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
    {
      var assemblyName = new AssemblyName(args.Name).Name + ".dll";
      var probePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libraries", assemblyName);
      return File.Exists(probePath) ? Assembly.LoadFrom(probePath) : null;
    };

    ApplicationConfiguration.Initialize();

    // Get the current state of the registry
    bool LocalRegistry = true;
    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(State.REGKEY))
    {
      if (key is null) LocalRegistry = false;
    }
    bool GlobalRegistry = false;
    using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(
      @"Software\Microsoft\Windows\CurrentVersion\Run"))
    {
      string regValue = (string)(key?.GetValue("AutoLogout") ?? "");
      if (regValue.Contains(Common.exePath)) GlobalRegistry = true;
    }

    // Handle special parameters
    if (args.Contains("--register"))
    {
      // Register AutoLogout to start automatically on login
      // This requires an Admin privileges
      if (!Environment.IsPrivilegedProcess)
      {
        Common.RelaunchAsAdmin("--register");
        return;
      }
      Common.RegisterStartup(true);
      return;
    }
    if (args.Contains("--unregister"))
    {
      // Unregister AutoLogout so it no longer starts automatically on login
      // This requires an Admin privileges
      if (!Environment.IsPrivilegedProcess)
      {
        Common.RelaunchAsAdmin("--unregister");
        return;
      }
      Common.RegisterStartup(false);
      return;
    }
    if (args.Contains("--service"))
    {
      // The --service tag indicates that AutoLogout launched automatically from any account
      // Refuse to run if AutoLogout is not configured for this account
      if (!LocalRegistry) return;
      // Continue normal startup
    }

    // 
    if (!LocalRegistry && !GlobalRegistry)
    {
      // Run first time setup
      Application.Run(new FirstTime());
    }
    else
    {
      // Normal startup
      Application.Run(new CountdownTimer());
    }
  }
}