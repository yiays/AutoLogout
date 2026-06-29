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
    public SyncedState syncedState;
    public UserIntent userIntent = UserIntent.None;
    public bool Paused = false;
    public bool graceGiven = false;
    public int tempTimeLimit = -1; // This stores temporary overrides to the time limit. Takes priority over bedtime
    public int bedtimeTimeLimit = -1; // This stores bedtime-related overrides to the time limit
    private int realTimeLimit
    {
        get => tempTimeLimit != -1 ? tempTimeLimit : bedtimeTimeLimit != -1 && bedtimeTimeLimit < syncedState.todayTimeLimit ? bedtimeTimeLimit : syncedState.todayTimeLimit;
    }
    public int remainingTime
    {
        get => realTimeLimit == -1 ? -1 : Math.Max(realTimeLimit - syncedState.usedTime, 0);
    }

    public State()
    {
        var loader = OS.Current.LoadState();
        loader.Wait();
        syncedState = loader.Result;
    }

    public async Task NewPassword(string password)
    {
        syncedState.hashedPassword = BC.BCrypt.HashPassword(password);
        await OS.Current.SaveState(syncedState);
    }
    public bool CheckPassword(string password)
    {
        return BC.BCrypt.Verify(password, syncedState.hashedPassword);
    }
}