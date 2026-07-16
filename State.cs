using System;
using System.Threading.Tasks;
using BC = BCrypt.Net;

namespace AutoLogout;

public enum UserIntent { None, Exit };

public class DeltaState
{
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
    public SyncedState()
    {
        if(DateOnly.FromDateTime(DateTime.Today) != usageDate)
            todayTimeLimit = dailyTimeLimit;
    }
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

public class State
{
    public event Action? Changed;
    public SyncedState state;
    public UserIntent userIntent = UserIntent.None;
    public bool Paused = false;
    public bool graceGiven = false;
    public int? tempTimeLimit = null; // This stores temporary overrides to the time limit. Takes priority over bedtime
    public int? bedtimeTimeLimit = null; // This stores bedtime-related overrides to the time limit
    private int? RealTimeLimit
    {
        get
        {
            // First check all overrides
            if(tempTimeLimit is not null)
            {
                if(tempTimeLimit == -1) return null;
                return tempTimeLimit;
            }
            else if(bedtimeTimeLimit is not null)
            {
                // Assume bedtimeTimeLimit is always positive
                if(bedtimeTimeLimit < state.todayTimeLimit) return bedtimeTimeLimit;
            }
            // Overrides don't apply, just return todayTimeLimit
            if(state.todayTimeLimit == -1) return null;
            return state.todayTimeLimit;
        }
    }
    public int? RemainingTime
    {
        get
        {
            var realtime = RealTimeLimit;
            var bedtime = TimeUntilBedtime;
            if(realtime is null) return null;
            var usedtime = realtime - state.usedTime;
            if(bedtime is null || usedtime < bedtime) return Math.Max((int)usedtime, 0);
            return Math.Max((int)bedtime, 0);
        }
    }
    public int? TimeUntilBedtime
    {
        get
        {
            if(state.bedtime == state.waketime) return null; // No bedtime
            DateTime now = DateTime.Now;
            DateTime nextBedTime = new(now.Year, now.Month, now.Day, state.bedtime.Hour, state.bedtime.Minute, 0);
            DateTime todayWakeTime = new(now.Year, now.Month, now.Day, state.waketime.Hour, state.waketime.Minute, 0);
            if(now > todayWakeTime && todayWakeTime > nextBedTime) nextBedTime.AddDays(1);
            return (int)(nextBedTime - now).TotalSeconds;
        }
    }

    public State()
    {
        try
        {
            var loader = OS.Current.LoadState();
            loader.Wait();
            state = loader.Result;
        }
        catch (Exception ex)
        {
            Console.Write(ex);
            state = new SyncedState();
        }

        if(state.uuid == Guid.Empty)
        {
            state.uuid = Guid.NewGuid();
        }
    }

    public void Tick(object? sender, EventArgs e)
    {
        if(Paused) return;
        state.usedTime++;
        Changed?.Invoke();
    }
    public void TogglePause()
    {
        Paused = !Paused;
        Changed?.Invoke();
    }
    public async Task NewPassword(string password)
    {
        state.hashedPassword = BC.BCrypt.HashPassword(password);
        await OS.Current.SaveState(state);
    }
    public bool CheckPassword(string password)
    {
        if(password.Length == 0) return false;
        return BC.BCrypt.Verify(password, state.hashedPassword);
    }
}