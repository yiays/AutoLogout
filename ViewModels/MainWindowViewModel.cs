using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoLogout.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private string _timeDisplay = "No limit";

    public string TimeDisplay
    {
        get => _timeDisplay;
        set => SetField(ref _timeDisplay, value);
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