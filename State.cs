using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using BC = BCrypt.Net;
using Avalonia.Media.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace AutoLogout;

public enum UserIntent { None, Setup, Parent, Grace, Exit }
public enum UpdateUrgency { None = 0, Feature = 1, Critical = 2 }

public class UsageEntry
{
    public HashSet<string> names { get; set; } = [];
    public int usedTime { get; set; } = 0;
}
public class UsageDate
{
    public int? totalUsage { get; set; }
    [JsonPropertyName("entries")]
    public Dictionary<string, UsageEntry> Entries { get; set; } = [];
}
public class UsageRecord: SortedDictionary<DateOnly, UsageDate>;

/// <summary>
/// Contains all state which can be changed or sent externally (from syncing or ControlPanel)
/// </summary>
public class DeltaState
{
    public Guid? authKey { get; set; }
    public Guid? uuid { get; set; }
    public int? dailyTimeLimit { get; set; }
    public int? todayTimeLimit { get; set; }
    public int? usedTime { get; set; }
    public DateOnly? usageDate { get; set; }
    public UsageRecord? usage { get; set; }
    public TimeOnly? bedtime { get; set; }
    public TimeOnly? waketime { get; set; }
    public Guid? syncAuthor { get; set; }
}

/// <summary>
/// Contains all state which is stored
/// </summary>
public class StoredState : DeltaState
{
    public new Guid authKey = Guid.Empty;
    public new Guid uuid = Guid.Empty;
    public bool Online = false;
    public string hashedPassword = "";
    public new int dailyTimeLimit = -1;
    public new int todayTimeLimit = -1;
    public new int usedTime = 0;
    public new DateOnly usageDate = DateOnly.FromDateTime(DateTime.Today);
    public new UsageRecord usage = [];
    public Dictionary<string,string> appIcons = [];
    public new TimeOnly bedtime = new(0, 0);
    public new TimeOnly waketime = new(0, 0);
    public new Guid? syncAuthor = null;

    /// <summary>
    /// Apply new changes to state from an external source (like syncing)
    /// </summary>
    public void Update(DeltaState delta)
    {
        dailyTimeLimit = delta.dailyTimeLimit ?? dailyTimeLimit;
        todayTimeLimit = delta.todayTimeLimit ?? todayTimeLimit;
        usedTime = delta.usedTime ?? usedTime;
        usageDate = delta.usageDate ?? usageDate;
        bedtime = delta.bedtime ?? bedtime;
        waketime = delta.waketime ?? waketime;
        syncAuthor = delta.syncAuthor;
    }
    /// <summary>
    /// Removes old and redundant data from state
    /// </summary>
    public void Clean()
    {
        var lastMonth = new DateOnly().AddMonths(-1);
        var keysToRemove = usage.Keys
            .Where(key => key < lastMonth)
            .ToList();
        foreach(var key in keysToRemove)
            usage.Remove(key);
    }
}

/// <summary>
/// Rules and logic for the state of the entire app
/// </summary>
public class AppState
{
    private readonly UserIntent[] AuthIntents = [UserIntent.Parent, UserIntent.Setup];
    public event Action? Changed;
    public event Action? UpdateAvailable; //TODO: add update banners to FirstTimeSetup and ControlPanel
    public StoredState Store = new();
    public Dictionary<string, Bitmap> IconRepo = [];
    public UserIntent Intent = UserIntent.None;
    public bool Paused = false;
    public string Version { get {
            var result = Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "0.0.0";
            if(result.IndexOf('+')>0)
                result = result[..result.IndexOf('+')];
            return result;
        } }
    public UpdateUrgency Update { get; set {
            // Only higher values can be set
            if(value > field) {
                field = value;
                UpdateAvailable?.Invoke();
            }
        } } = UpdateUrgency.None;
    public string? UpdateName;
    /// <summary>
    /// Provides a different update url depending on whether this is a pre-release
    /// </summary>
    public string UpdateUrl
    {
#if DEBUG
        get => field + "#preview";
#else
        get => field + "#stable";
#endif
        set;
    } = "https://autologout.yiays.com/download/";
    /// <summary>
    /// This stores temporary overrides to the time limit. Takes priority over bedtime
    /// </summary>
    public int? tempTimeLimit = null;
    /// <summary>
    /// Considers all rules, returns a positive whole number, or null if there are no rules
    /// </summary>
    public int? RemainingTime
    {
        get
        {
            // First check temp override
            if(tempTimeLimit is not null)
            {
                if(tempTimeLimit == -1) return null;
                return Math.Max((int)(tempTimeLimit - Store.usedTime), 0);
            }

            // Otherwise calculate time limit including bedtime
            int? timelimit = Store.todayTimeLimit == -1? null: Store.todayTimeLimit;
            var bedtime = TimeUntilBedtime;
            if(timelimit is null) return null;
            var usedtime = timelimit - Store.usedTime;
            if(bedtime is null || usedtime < bedtime) return Math.Max((int)usedtime, 0);
            return Math.Max((int)bedtime, 0);
        }
    }
    /// <summary>
    /// Determines when bedtime is next.
    /// Returns a negative number if it is currently bedtime, returns null if there is no bedtime.
    /// </summary>
    public int? TimeUntilBedtime
    {
        get
        {
            if(Store.bedtime == Store.waketime) return null; // No bedtime
            DateTime now = DateTime.Now;
            DateTime nextBedTime = new(now.Year, now.Month, now.Day, Store.bedtime.Hour, Store.bedtime.Minute, 0);
            DateTime todayWakeTime = new(now.Year, now.Month, now.Day, Store.waketime.Hour, Store.waketime.Minute, 0);
            if(now > todayWakeTime && todayWakeTime > nextBedTime) nextBedTime.AddDays(1);
            return (int)(nextBedTime - now).TotalSeconds;
        }
    }

    public void Load()
    {
        try
        {
            var loader = OS.Current.LoadState();
            loader.Wait();
            Store = loader.Result;
        }
        catch (Exception ex)
        {
            // If loading fails for any reason, fall back to defaults
            Console.Write(ex);
        }

        // UUID must be unique
        if(Store.uuid == Guid.Empty)
        {
            Store.uuid = Guid.NewGuid();
        }
        // Password must be set to continue, throw the user into setup mode
        if(Store.hashedPassword.Length == 0)
        {
            Intent = UserIntent.Setup;
        }
    }

    /// <summary>
    /// Loading components that require Avalonia to be running first
    /// </summary>
    public void OnReady()
    {
        foreach (var kvp in Store.appIcons)
        {
            byte[] bytes = Convert.FromBase64String(kvp.Value);
            using var memoryStream = new MemoryStream(bytes);
            IconRepo[kvp.Key] = new Bitmap(memoryStream);
        }
    }

    /// <summary>
    /// Call once every second to update RemainingTime, notifies Changed once tick is complete
    /// </summary>
    public void Tick(object? sender, EventArgs e)
    {
        // Don't progress time if the timer is paused
        if(Paused) return;

        // Also don't progress time if the userintent is in an authorized state
        if(AuthIntents.Contains(Intent)) return;

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Reset usage time whenever a new day starts
        if(today != Store.usageDate)
        {
            Store.todayTimeLimit = Store.dailyTimeLimit;
            Store.usedTime = 0;
            Store.usageDate = today;
        }
        
        // Count every second that the device is used
        Store.usedTime++;

        // Log focused app usage if usedTime is divisible by 10
        if(Store.usedTime % 10 == 0)
        {
            if(!Store.usage.ContainsKey(today)) Store.usage[today].Entries = [];
            Store.usage[today].totalUsage = Store.usedTime;
            if(OS.Current.GetFocused() is FocusedWindow window) {
                if(!Store.usage[today].Entries.ContainsKey(window.exeName))
                    Store.usage[today].Entries[window.exeName] = new UsageEntry
                    {
                        names = [window.windowName],
                        usedTime = 10
                    };
                else {
                    Store.usage[today].Entries[window.exeName].names.Add(window.windowName);
                    Store.usage[today].Entries[window.exeName].usedTime += 10;
                }
            }
            OS.Current.SaveState();
        }

        // Notify that the state has changed
        Changed?.Invoke();
    }
    public void AcceptDelta(DeltaState delta)
    {
        Store.Update(delta);
        Changed?.Invoke();
    }
    public void TogglePause()
    {
        Paused = !Paused;
        Changed?.Invoke();
    }
    public async Task NewPassword(string password)
    {
        Store.hashedPassword = BC.BCrypt.HashPassword(password);
        await OS.Current.SaveState();
    }
    public async Task AddIcon(string exeName, MemoryStream memoryStream)
    {
        memoryStream.Position = 0;
        State.Current.IconRepo[exeName] = new Bitmap(memoryStream);
        memoryStream.Position = 0;
        State.Current.Store.appIcons[exeName] = Convert.ToBase64String(memoryStream.GetBuffer());
        await OS.Current.SaveState();
        Console.WriteLine($"Saved icon for {exeName}");
    }
    public bool CheckPassword(string password)
    {
        if(password.Length == 0) return false;
        // This should never happen, but if it somehow does, prevent a crash
        if(Store.hashedPassword.Length == 0) {
            Console.WriteLine("The password is unset somehow, relaunching to force user to set a new one.");
            Intent = UserIntent.Exit;
            OS.Current.Relaunch("");
            return false;
        }
        return BC.BCrypt.Verify(password, Store.hashedPassword);
    }
}

public static class State
{
    public static readonly AppState Current = new();
}