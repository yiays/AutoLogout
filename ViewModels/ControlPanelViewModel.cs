using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoLogout.ViewModels;

public class ControlPanelViewModel : INotifyPropertyChanged
{
    private string _usedTimeIndicator;
    private int _dailyLimit;
    private int _todayLimit;
    private TimeSpan _wakeTime;
    private TimeSpan _sleepTime;
    private bool _autostart;

    public string UsedTimeIndicator
    {
        get => _usedTimeIndicator;
        set => SetField(ref _usedTimeIndicator, value);
    }

    public int DailyLimit
    {
        get => _dailyLimit;
        set => SetField(ref _dailyLimit, value);
    }

    public int TodayLimit
    {
        get => _todayLimit;
        set => SetField(ref _todayLimit, value);
    }

    public TimeSpan WakeTime
    {
        get => _wakeTime;
        set => SetField(ref _wakeTime, value);
    }

    public TimeSpan SleepTime
    {
        get => _sleepTime;
        set => SetField(ref _sleepTime, value);
    }

    public bool Autostart
    {
        get => _autostart;
        set => SetField(ref _autostart, value);
    }

    public ControlPanelViewModel()
    {
        // Initialize default values
        UsedTimeIndicator = "0 minutes";
        DailyLimit = 480; // Example: 8 hours
        TodayLimit = 480;
        WakeTime = TimeSpan.FromHours(8);
        SleepTime = TimeSpan.FromHours(22);
        Autostart = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
