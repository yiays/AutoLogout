using System;
using System.Threading.Tasks;
using BC = BCrypt.Net;

namespace AutoLogout;

public enum UserIntent { None, Setup, Parent, Grace, Exit };

public class DeltaState
{
    /// <summary>
    /// Contains all state which can be changed externally (from syncing or ControlPanel)
    /// </summary>
    public Guid? uuid;
    public int? dailyTimeLimit;
    public int? todayTimeLimit;
    public int? usedTime;
    public DateOnly? usageDate;
    public TimeOnly? bedtime;
    public TimeOnly? waketime;
    public Guid? syncAuthor;
}

public class SyncedState : DeltaState
{
    /// <summary>
    /// Contains all state which is stored and synced
    /// </summary>
    public Guid authKey = Guid.Empty;
    public new Guid uuid = Guid.Empty;
    public bool Online = false;
    public string hashedPassword = "";
    public new int dailyTimeLimit = -1;
    public new int todayTimeLimit = -1;
    public new int usedTime = 0;
    public new DateOnly usageDate = DateOnly.FromDateTime(DateTime.Today);
    public new TimeOnly bedtime = new(0, 0);
    public new TimeOnly waketime = new(0, 0);
    public new Guid? syncAuthor = null;
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
}

public class AppState
{
    /// <summary>
    /// Rules and logic for the state of the entire app
    /// </summary>
    private readonly UserIntent[] AuthIntents = [UserIntent.Parent, UserIntent.Setup];
    public event Action? Changed;
    public SyncedState Store = new();
    public UserIntent Intent = UserIntent.None;
    public bool Paused = false;
    public int? tempTimeLimit = null; // This stores temporary overrides to the time limit. Takes priority over bedtime
    private int? RealTimeLimit
    {
        get
        {
            // First check temp override
            if(tempTimeLimit is not null)
            {
                if(tempTimeLimit == -1) return null;
                return tempTimeLimit;
            }
            // Override didn't apply, just return todayTimeLimit
            if(Store.todayTimeLimit == -1) return null;
            return Store.todayTimeLimit;
        }
    }
    public int? RemainingTime
    {
        get
        {
            var realtime = RealTimeLimit;
            var bedtime = TimeUntilBedtime;
            if(realtime is null) return null;
            var usedtime = realtime - Store.usedTime;
            if(bedtime is null || usedtime < bedtime) return Math.Max((int)usedtime, 0);
            return Math.Max((int)bedtime, 0);
        }
    }
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
            Console.Write(ex);
        }

        if(Store.uuid == Guid.Empty)
        {
            Intent = UserIntent.Setup; //TODO: handle this elsewhere
            Store.uuid = Guid.NewGuid();
        }
    }

    public void Tick(object? sender, EventArgs e)
    {
        // Don't progress time if the timer is paused
        if(Paused) return;

        // Also don't progress time if the userintent is in an authorized state
        if(AuthIntents.Contains(Intent)) return;

        // Reset usage time whenever a new day starts
        if(DateOnly.FromDateTime(DateTime.Today) != Store.usageDate)
        {
            Store.todayTimeLimit = Store.dailyTimeLimit;
            Store.usedTime = 0;
            Store.usageDate = DateOnly.FromDateTime(DateTime.Today);
        }
        
        // Count every second that the device is used
        Store.usedTime++;

        // Notify that the state has changed
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
    public bool CheckPassword(string password)
    {
        if(password.Length == 0) return false;
        return BC.BCrypt.Verify(password, Store.hashedPassword);
    }
}

public static class State
{
    public static readonly AppState Current = new();
}