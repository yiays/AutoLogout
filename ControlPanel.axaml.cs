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

    public int UsedTime { get => state.state.usedTime; }
    public int DailyLimit { get => state.state.dailyTimeLimit; }
    public int TodayLimit { get => state.state.todayTimeLimit; }
    public TimeSpan Bedtime { get => state.state.bedtime.ToTimeSpan(); }
    public TimeSpan Waketime { get => state.state.waketime.ToTimeSpan(); }
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
        if(parent is null) return;

        string qrContent = $"https://autologout.yiays.com/app/addAccount?uuid={parent.state.state.uuid}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        var memStream = new MemoryStream(qrCode.GetGraphic(1));

        QRCode = new Bitmap(memStream);
        ImageQRCode.Source = QRCode;
        InvalidateVisual();

        tabControl.SelectedIndex = 3;
    }
    private void DeauthButton_Click(object sender, RoutedEventArgs e)
    {
        //TODO
    }
    private void AutoStart_Checked(object sender, RoutedEventArgs e)
    {
        //TODO
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
        OS.Current.SaveState(state.state);
        parent?.state.state = state.state;
        Close();
    }
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}