using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Microsoft.Win32;
using QRCoder;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace AutoLogout;

public partial class ControlPanel : Window
{
    // UAC shield - TODO: Implement in Avalonia
    // private const int BCM_SETSHIELD = 0x160C;
    // [DllImport("user32.dll", CharSet = CharSet.Auto)]
    // private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private readonly MainWindow parent;
    private readonly bool autostartEnabled = false;

    public ControlPanel(MainWindow parent)
    {
        this.parent = parent;
        Title = "AutoLogout Settings";
        Icon = new WindowIcon("Resources/icon-light.ico");
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        CanResize = false;
        ShowInTaskbar = true;
        SizeToContent = SizeToContent.WidthAndHeight;

        InitializeComponent();

        // Set up controls
        dailylimitPicker.Minimum = -1;
        dailylimitPicker.Maximum = 1440;
        todaylimitPicker.Minimum = -1;
        todaylimitPicker.Maximum = 1440;
        autostartCheckBox.IsChecked = autostartEnabled;

        // Events
        autostartCheckBox.IsCheckedChanged += autostartCheckBox_Checked;
        AuthButton.Click += AuthButton_Click;
        DeauthButton.Click += DeauthButton_Click;
        ChangePasswordButton.Click += ChangePasswordButton_Click;
        RemoveAccountButton.Click += RemoveAccountButton_Click;
        SaveButton.Click += SaveButton_Click;
        CancelButton.Click += (s, e) => Close();

        using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(
          @"Software\Microsoft\Windows\CurrentVersion\Run"))
        {
            string regValue = (string)(key?.GetValue("AutoLogout") ?? "");
            if (regValue.Contains(Common.exePath)) autostartEnabled = true;
        }

        // Set up controls
        dailylimitPicker.Minimum = -1;
        dailylimitPicker.Maximum = 1440;
        todaylimitPicker.Minimum = -1;
        todaylimitPicker.Maximum = 1440;
        autostartCheckBox.IsChecked = autostartEnabled;

        // Events
        autostartCheckBox.IsCheckedChanged += autostartCheckBox_Checked;
        AuthButton.Click += AuthButton_Click;
        DeauthButton.Click += DeauthButton_Click;
        ChangePasswordButton.Click += ChangePasswordButton_Click;
        RemoveAccountButton.Click += RemoveAccountButton_Click;
        SaveButton.Click += SaveButton_Click;
        CancelButton.Click += (s, e) => Close();

        // Initialize controls with current state
        OnStateChanged();
        // Subscribe to future state changes
        parent.state.Changed += OnStateChanged;
    }

    private void OnStateChanged()
    {
        // Update all controls that are affected by the state
        Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(parent.state.usedTime);
            string timeString = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
            usedTimeIndicator.Text = timeString;

            dailylimitPicker.Value = parent.state.dailyTimeLimit >= 0 ? parent.state.dailyTimeLimit / 60 : -1;
            todaylimitPicker.Value = parent.state.todayTimeLimit >= 0 ? parent.state.todayTimeLimit / 60 : -1;

            TimeOnly waketime = parent.state.waketime;
            waketimePicker.SelectedTime = waketime.ToTimeSpan();

            TimeOnly bedtime = parent.state.bedtime;
            sleeptimePicker.SelectedTime = bedtime.ToTimeSpan();

            DeauthButton.IsEnabled = parent.state.OnlineMode;
        });
    }

    private void autostartCheckBox_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // TODO: Implement UAC shield in Avalonia
        // if (autostartCheckBox.IsChecked != autostartEnabled)
        //     SendMessage(SaveButton.Handle, BCM_SETSHIELD, IntPtr.Zero, new IntPtr(1));
        // else
        //     SendMessage(SaveButton.Handle, BCM_SETSHIELD, IntPtr.Zero, new IntPtr(0));
    }

    // TODO: Add AuthButton, ChangePasswordButton, RemoveAccountButton to XAML and wire events

    private void AuthButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!parent.state.OnlineMode)
        {
            parent.state.OnlineMode = true;
            Task.Run(parent.state.Sync);
            parent.state.TriggerStateChanged();
        }

        // Generate a QR code for the user to scan with their phone
        string qrContent = $"https://autologout.yiays.com/app/addAccount?uuid={parent.state.uuid}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new QRCode(qrData);
        using var qrBitmap = qrCode.GetGraphic(20);

        // Convert to Avalonia Bitmap
        using var memory = new MemoryStream();
        qrBitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
        memory.Position = 0;
        var bitmap = new Bitmap(memory);

        // Show QR in a new window
        var qrWindow = new Window
        {
            Title = "Scan this QR code with your phone",
            Width = 400,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Image { Source = bitmap, Stretch = Avalonia.Media.Stretch.Uniform }
        };
        qrWindow.ShowDialog(this);
    }

    private async void DeauthButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var result = await MessageBoxManager.GetMessageBoxStandard(
            "AutoLogout",
            "Are you sure you want delete all your online data and sign out all devices?",
            ButtonEnum.YesNo,
            MsBox.Avalonia.Enums.Icon.Warning
        ).ShowAsync();
        if (result == ButtonResult.Yes)
        {
            await parent.state.Deauth();
        }
    }

    private async void ChangePasswordButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (await parent.state.NewPassword())
        {
            await MessageBoxManager.GetMessageBoxStandard(
                "AutoLogout",
                "Password changed successfully.",
                ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Info
            ).ShowAsync();
        }
    }

    private async void RemoveAccountButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var result = await MessageBoxManager.GetMessageBoxStandard(
            "AutoLogout",
            "This disables AutoLogout for this account, any other accounts are unaffected. You can reactivate AutoLogout anytime by opening it again.\nDo you want to continue?",
            ButtonEnum.YesNo,
            MsBox.Avalonia.Enums.Icon.Warning
        ).ShowAsync();
        if (result == ButtonResult.Yes)
        {
            if (parent.state.OnlineMode)
                await parent.state.Deauth();
            State.ClearRegistry();
            parent.state.ExitIntent = true;
            // Close the main window to exit
            var mainWindow = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            mainWindow?.Close();
        }
    }

    private async void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        parent.state.dailyTimeLimit = (int)(dailylimitPicker.Value >= 0 ? dailylimitPicker.Value * 60 : -1);
        parent.state.todayTimeLimit = (int)(todaylimitPicker.Value >= 0 ? todaylimitPicker.Value * 60 : -1);
        // Give parents some time to correct their mistake if they set the time limit too low
        if (parent.state.remainingTime == 0)
            parent.state.todayTimeLimit = parent.state.usedTime + 30;
        parent.state.waketime = TimeOnly.FromTimeSpan(waketimePicker.SelectedTime ?? TimeSpan.Zero);
        parent.state.bedtime = TimeOnly.FromTimeSpan(sleeptimePicker.SelectedTime ?? TimeSpan.Zero);
        await parent.state.SaveToRegistry();
        parent.state.TriggerStateChanged();

        if (autostartCheckBox.IsChecked != autostartEnabled)
        {
            if (autostartCheckBox.IsChecked == true)
                Common.Relaunch("--register");
            else
                Common.Relaunch("--unregister");
        }

        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        parent.state.Changed -= OnStateChanged;
        base.OnClosed(e);
        parent.controlPanel = null;
    }
}