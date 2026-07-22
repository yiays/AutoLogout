using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using QRCoder;

namespace AutoLogout;

public partial class ControlPanel : Window
{
    public MainWindow? parent;
    public State state;

    public string UsedTime { get; set; } = "Loading...";
    public int DailyLimit { get; set; }
    public int TodayLimit { get; set; }
    public TimeSpan Bedtime { get; set; }
    public TimeSpan Waketime { get; set; }
    public bool AutoStart { get => OS.Current.AutoStart; }
    public Bitmap? QRCode { get; set; }

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
        SetFields();
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
            SetFields();
            InvalidateVisual();
        }
    }
    private void SetFields()
    {
        var timeSpan = TimeSpan.FromSeconds((int)state.state.usedTime);
        UsedTime = string.Format("{0:D2}:{1:D2}.{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
        DailyLimit = state.state.dailyTimeLimit / 60;
        TodayLimit = state.state.todayTimeLimit / 60;
        Bedtime = state.state.bedtime.ToTimeSpan();
        Waketime = state.state.waketime.ToTimeSpan();
    }

    private void AuthButton_Click(object sender, RoutedEventArgs e)
    {
        if(parent is null) return;

        //TODO: perform first API sync before showing QR code

        string qrContent = $"https://autologout.yiays.com/app/addAccount?uuid={parent.state.state.uuid}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        var memStream = new MemoryStream(qrCode.GetGraphic(1));

        QRCode = new Bitmap(memStream);
        ImageQRCode.Source = QRCode;
        InvalidateVisual();

        // Switch to the hidden tab
        tabControl.SelectedIndex = 3;
    }
    private async void DeauthButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new ConfirmDialog(
            "This will sign out all AutoLogout Manager instances and disable syncing. Continue?",
            "Disconnect all devices"
        );
        await confirm.ShowDialog(this);
        if(confirm.Result ?? false)
        {
            //TODO: delete API account and disable syncing
        }
    }
    private async void AutoStart_Checked(object sender, RoutedEventArgs e)
    {
        if(sender is CheckBox checkBox)
        {
            var enable = checkBox.IsChecked ?? false;
            if(enable == AutoStart) return; // Nothing will be changed

            var confirm = new ConfirmDialog(
                enable?
                    "AutoLogout will start automatically on any user accounts that have set up a parent password. Continue?"
                :
                    "This will prevent AutoLogout from auto-starting on all user accounts on this machine. Continue?",
                "Start automatically on login"
            );
            await confirm.ShowDialog(this);
            if(confirm.Result ?? false)
            {
                var success = await OS.Current.RelaunchAsAdmin(enable? "--register": "--unregister");
                if (!success)
                {
                    // Revert checkbox state
                    checkBox.IsChecked = !enable;
                    checkBox.InvalidateVisual();
                }
            }
            else
            {
                // Revert checkbox state
                checkBox.IsChecked = !enable;
                checkBox.InvalidateVisual();
            }
        }
    }
    private async void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        var prompt = new PromptDialog("Enter a new parent password.", "Change parent password", true);
        await prompt.ShowDialog(this);
        var result = prompt.Result;
        if(result is not null)
        {
            if(result.Length > 0)
            {
                parent?.state.NewPassword(result);
                var alert = new AlertDialog("Parent password updated!", "Success");
                await alert.ShowDialog(this);
            }
            else
            {
                var alert = new AlertDialog("Parent password must not be empty!", "Error");
                await alert.ShowDialog(this);
            }
        }
    }
    private async void RemoveControls_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new ConfirmDialog("This will disable AutoLogout entirely for this account. Continue?", "Remove AutoLogout from this account");
        await confirm.ShowDialog(this);
        if(confirm.Result ?? false)
        {
            await OS.Current.ClearState();
            var alert = new AlertDialog("AutoLogout has been removed from this account. Closing now.", "AutoLogout");
            await alert.ShowDialog(this);
            parent?.state.userIntent = UserIntent.Exit;
            Close();
        }
    }
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if(parent is null) return;
        parent.state.state.Update(new DeltaState
        {
            todayTimeLimit = TodayLimit * 60,
            dailyTimeLimit = DailyLimit * 60,
            bedtime = TimeOnly.FromTimeSpan(Bedtime),
            waketime = TimeOnly.FromTimeSpan(Waketime),
        });
        OS.Current.SaveState(parent.state.state);
        Close();
    }
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}