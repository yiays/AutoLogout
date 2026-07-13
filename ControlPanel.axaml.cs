using System;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AutoLogout;

public partial class ControlPanel : Window
{
    public MainWindow? parent;
    public State state;

    int UsedTime { get => state.state.usedTime; }
    int DailyLimit { get => state.state.dailyTimeLimit; }
    int TodayLimit { get => state.state.todayTimeLimit; }
    int Downtime { get => state.state.bedtime.Hour; }
    bool AutoStart { get => OS.Current.AutoStart; }

    public ControlPanel()
    {
        state = new State();
        InitializeComponent();
        DataContext = this;
    }
    public ControlPanel(MainWindow parent, State state)
    {
        this.parent = parent;
        this.state = state;
        InitializeComponent();
        DataContext = this;

        // Events
        parent.state.Changed += OnChanged;
    }

    private void OnChanged()
    {
        // Discard current settings if new settings come in remotely
        if (parent?.state.state.syncAuthor.HasValue is true)
        {
            state.state = parent.state.state;
            InvalidateVisual();
        }
    }

    private void AuthButton_Click(object sender, RoutedEventArgs e)
    {
        //TODO
    }
    private void DeauthButton_Click(object sender, RoutedEventArgs e)
    {
        //TODO
    }
    private void AutoStart_Checked(object sender, RoutedEventArgs e)
    {
        //TODO
    }
    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        //TODO
    }
    private void RemoveControls_Click(object sender, RoutedEventArgs e)
    {
        //TODO
    }
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        OS.Current.SaveState(state.state);
        parent?.state.state = state.state;
        Close();
    }
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}