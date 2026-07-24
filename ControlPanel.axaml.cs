using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using QRCoder;

namespace AutoLogout;

public partial class ControlPanel : Window
{
    public string UsedTime { get; set; } = "Loading...";
    public int DailyLimit { get; set; }
    public int TodayLimit { get; set; }
    public TimeSpan Bedtime { get; set; }
    public TimeSpan Waketime { get; set; }
    public bool AutoStart { get => OS.Current.AutoStart; }
    public Bitmap? QRCode { get; set; }

    public ControlPanel() : this(null)
    {
        SetFields();
        InitializeComponent();
        DataContext = this;

        // Events
        State.Current.Changed += OnChanged;
    }
    public ControlPanel(string? soleTab)
    {
        /// Create an instance of ControlPanel where only one tab is available
        
        if(soleTab is not null)
        {
            TabLimits.IsEnabled = false;
            TabSync.IsEnabled = false;
            TabSystem.IsEnabled = false;
            var tab = this.FindControl<TabItem>(soleTab)
                      ?? throw new Exception("soleTab must exist");
            tabControl.TabIndex = tab.TabIndex;
        }
    }
    private void OnChanged()
    {
        /// Discard current settings if new settings come in remotely
        if (State.Current.Store.syncAuthor.HasValue is true)
        {
            SetFields();
            InvalidateVisual();
        }
    }
    private void SetFields()
    {
        var timeSpan = TimeSpan.FromSeconds(State.Current.Store.usedTime);
        UsedTime = string.Format("{0:D2}:{1:D2}.{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
        DailyLimit = State.Current.Store.dailyTimeLimit / 60;
        TodayLimit = State.Current.Store.todayTimeLimit / 60;
        Bedtime = State.Current.Store.bedtime.ToTimeSpan();
        Waketime = State.Current.Store.waketime.ToTimeSpan();
    }

    private void AuthButton_Click(object? sender, RoutedEventArgs e)
    {
        //TODO: perform first API sync before showing QR code

        string qrContent = $"https://autologout.yiays.com/app/addAccount?uuid={State.Current.Store.uuid}";

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
    private async void DeauthButton_Click(object? sender, RoutedEventArgs e)
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
    private async void AutoStart_Checked(object? sender, RoutedEventArgs e)
    {
        if(sender is CheckBox checkBox)
        {
            var enable = checkBox.IsChecked ?? false;
            if(enable == AutoStart) return; // Nothing will be changed

            var result = await SetAutoStart(enable);
            if (!result)
            {
                // Revert checkbox state
                checkBox.IsChecked = !enable;
                checkBox.InvalidateVisual();
            }
        }
    }
    public async Task<bool> SetAutoStart(bool enable)
    {
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
            return await OS.Current.RelaunchAsAdmin(enable? "--register": "--unregister");
        }
        return false;
    }
    private async void ChangePassword_Click(object? sender, RoutedEventArgs e)
    {
        await ChangePassword();
    }
    public async Task<bool> ChangePassword()
    {
        var prompt = new PromptDialog("Enter a new parent password.", "Change parent password", true);
        await prompt.ShowDialog(this);
        var result = prompt.Result;
        if(result is not null)
        {
            if(result.Length > 0)
            {
                await State.Current.NewPassword(result);
                var alert = new AlertDialog("Parent password updated!", "Success");
                await alert.ShowDialog(this);
                return true;
            }
            else
            {
                var alert = new AlertDialog("Parent password must not be empty!", "Error");
                await alert.ShowDialog(this);
            }
        }
        return false;
    }
    private async void RemoveControls_Click(object? sender, RoutedEventArgs e)
    {
        var confirm = new ConfirmDialog("This will disable AutoLogout entirely for this account. Continue?", "Remove AutoLogout from this account");
        await confirm.ShowDialog(this);
        if(confirm.Result ?? false)
        {
            await OS.Current.ClearState();
            var alert = new AlertDialog("AutoLogout has been removed from this account. Closing now.", "AutoLogout");
            await alert.ShowDialog(this);
            State.Current.Intent = UserIntent.Exit;
            Close();
        }
    }
    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        State.Current.Store.Update(new DeltaState
        {
            todayTimeLimit = TodayLimit * 60,
            dailyTimeLimit = DailyLimit * 60,
            bedtime = TimeOnly.FromTimeSpan(Bedtime),
            waketime = TimeOnly.FromTimeSpan(Waketime),
        });
        OS.Current.SaveState();
        Close();
    }
    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}