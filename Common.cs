using System.Diagnostics;
using Avalonia.Controls.Notifications;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Microsoft.Win32;
using BC = BCrypt.Net;

namespace AutoLogout
{
  public static class Common
  {
    public static WindowNotificationManager notificationManager = new();
    public static string exePath
    {
      get
      {
        return Process.GetCurrentProcess().MainModule?.FileName
          ?? throw new Exception("Unable to get current executable name.");
      }
    }
    public static void Relaunch(string args)
    {
      var startInfo = new ProcessStartInfo(exePath)
      {
        UseShellExecute = true,
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
    public static void RelaunchAsAdmin(string args)
    {
      var startInfo = new ProcessStartInfo(exePath)
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

    public static void RegisterStartup(bool enable)
    {
      string appName = "AutoLogout";
      using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
        @"Software\Microsoft\Windows\CurrentVersion\Run", true
      );
      if (key is null) throw new Exception("System startup registry doesn't exist!");
      if (enable)
      {
        key.SetValue(appName, $"\"{exePath}\" --service");
        Notification notif = new() {
          Title = "AutoLogout Setup",
          Message = "AutoLogout has been configured to start on login.",
        };
        notificationManager.Show(notif);
      }
      else
      {
        key.DeleteValue(appName, false);
        Notification notif = new() {
          Title = "AutoLogout Setup",
          Message = "AutoLogout will no longer start on login.",
        };
        notificationManager.Show(notif);
      }
    }
  }

  public class State
  {
#if DEBUG
    public static string REGKEY = "Software\\Yiays\\AutoLogout-Preview";
#else
    public static string REGKEY = "Software\\Yiays\\AutoLogout";
#endif
    public bool OnlineMode = false;
    public bool ExitIntent = false;
    public bool Paused = false;
    public Guid authKey = Guid.Empty;
    public Guid uuid = Guid.Empty;
    public string hashedPassword = "";
    public int dailyTimeLimit = -1;
    public int todayTimeLimit = -1;
    public int tempTimeLimit = -1; // This stores temporary overrides to the time limit. Takes priority over bedtime
    public int bedtimeTimeLimit = -1; // This stores bedtime-related overrides to the time limit
    private int realTimeLimit { get => tempTimeLimit != -1 ? tempTimeLimit : bedtimeTimeLimit != -1 && bedtimeTimeLimit < todayTimeLimit ? bedtimeTimeLimit : todayTimeLimit; }
    public int remainingTime { get => realTimeLimit == -1 ? -1 : Math.Max(realTimeLimit - usedTime, 0); }
    public int usedTime = 0;
    public DateOnly usageDate = DateOnly.FromDateTime(DateTime.Today);
    public TimeOnly bedtime = new TimeOnly(0, 0);
    public TimeOnly waketime = new TimeOnly(0, 0);
    public bool graceGiven = false;
    public Guid? syncAuthor = null;

    public event Action? Changed;

    public API api = new();

    public void NewGuid()
    {
      uuid = Guid.NewGuid();
    }

    public void TriggerStateChanged()
    {
      Changed?.Invoke();
    }

    public async Task<int> FromRegistry()
    {
      RegistryKey? key = Registry.CurrentUser.CreateSubKey(REGKEY, true);
      if (key == null)
      {
        await MessageBoxManager.GetMessageBoxStandard(
          "Error",
          "Unable to access settings.",
          ButtonEnum.Ok,
          Icon.Error
        ).ShowAsync();
        return -1;
      }

      // Load current app state from registry
      OnlineMode = bool.Parse((string)key.GetValue("OnlineMode", "false"));

      string? rawAuthKey = (string?)key.GetValue("authKey", null);
      authKey = rawAuthKey is null ? Guid.Empty : new Guid(rawAuthKey);

      string? rawGuid = (string?)key.GetValue("guid", null);
      uuid = rawGuid is null ? Guid.Empty : new Guid(rawGuid);

      hashedPassword = (string)key.GetValue("password", "");

      string bedtimeRaw = (string)key.GetValue("bedtime", "0:00");
      bedtime = TimeOnly.Parse(bedtimeRaw);
      string waketimeRaw = (string)key.GetValue("waketime", "0:00");
      waketime = TimeOnly.Parse(waketimeRaw);

      dailyTimeLimit = (int)key.GetValue("dailyTimeLimit", -1);
      usageDate = DateOnly.Parse((string)key.GetValue("usageDate", "1/01/0001"));
      todayTimeLimit = (int)key.GetValue("todayTimeLimit", -1);
      usedTime = (int)key.GetValue("usedTime", 0);

      return 0;
    }

    public async Task<int> SaveToRegistry()
    {
      RegistryKey? key = Registry.CurrentUser.CreateSubKey(REGKEY);
      if (key == null)
      {
        await MessageBoxManager.GetMessageBoxStandard(
          "Error",
          "Unable to save settings.",
          ButtonEnum.Ok,
          Icon.Error
        ).ShowAsync();
        ExitIntent = true;
        return -1;
      }

      key.SetValue("OnlineMode", OnlineMode);
      key.SetValue("authKey", authKey);
      key.SetValue("guid", uuid);
      key.SetValue("password", hashedPassword);
      key.SetValue("usageDate", DateOnly.FromDateTime(DateTime.Today));
      key.SetValue("dailyTimeLimit", dailyTimeLimit);
      key.SetValue("todayTimeLimit", todayTimeLimit);
      key.SetValue("usedTime", usedTime);
      key.SetValue("bedtime", bedtime);
      key.SetValue("waketime", waketime);

      return 0;
    }

    public static void ClearRegistry()
    {
      Registry.CurrentUser.DeleteSubKeyTree(REGKEY);
    }

    public void AcceptDelta(API.Delta delta)
    {
      // Update local state with server response
      dailyTimeLimit = delta.dailyTimeLimit ?? dailyTimeLimit;
      todayTimeLimit = delta.todayTimeLimit ?? todayTimeLimit;
      usedTime = delta.usedTime ?? usedTime;
      usageDate = delta.usageDate ?? usageDate;
      bedtime = delta.bedtime ?? bedtime;
      waketime = delta.waketime ?? waketime;
      graceGiven = delta.graceGiven ?? graceGiven;
      syncAuthor = delta.syncAuthor;
    }

    public async Task<bool> NewPassword()
    {
      string? newPassword = Prompt.ShowDialog("Enter a new parent password.", "AutoLogout", true);
      if (newPassword == null)
      {
        return false;
      }
      hashedPassword = BC.BCrypt.HashPassword(newPassword);
      await SaveToRegistry();
      return true;
    }
    public async Task<bool> CheckPassword()
    {
      string? password = Prompt.ShowDialog("Enter the parent password to continue.", "AutoLogout Settings", true);
      if (password == null) return false;
      if (BC.BCrypt.Verify(password, hashedPassword)) return true;
      else
      {
        await MessageBoxManager.GetMessageBoxStandard(
          "Error",
          "The password was incorrect",
          ButtonEnum.Ok,
          Icon.Error
        ).ShowAsync();
        return false;
      }
    }

    // API methods
    public async Task Sync()
    {
      if (OnlineMode)
      {
        await api.Sync(this);
      }
    }
    public async Task Deauth()
    {
      OnlineMode = false;
      if (!await api.Deauth(this))
      {
        OnlineMode = true;
      }
      else
      {
        Changed?.Invoke();
      }
      await SaveToRegistry();
    }
  }

  public static class Prompt
  {
    public static string? ShowDialog(string text, string caption, bool sensitive = false)
    {
      var prompt = new PromptDialog(text, caption, sensitive);
      prompt.ShowDialog((Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow);
      return prompt.Result;
    }
  }
}