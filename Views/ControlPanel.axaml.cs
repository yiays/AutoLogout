using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using QRCoder;
using Avalonia.Media;

namespace AutoLogout;

public class UsageData
{
    public required uint position { get; set; }
    public required string exeName { get; set; }
    public required string windowNames { get; set; }
    public required float usedTime { get; set; }
    public string usedTimeFormatted { get => TimeSpan.FromSeconds(usedTime).ToString(@"hh\:mm"); }
    public Bitmap? icon { get; set; }
}

public partial class ControlPanel : Window
{
    // TabLimits
    public int UsedTime { get; set; } = 0;
    public string UsedTime_Formatted { get; set; } = "Loading...";
    public int MaxTime { get; set; } = 0;
    public Brush ExceededTime_Colour { get; set; }
    public int DailyLimit { get; set; }
    public int TodayLimit { get; set; }
    public TimeSpan Bedtime { get; set; }
    public TimeSpan Waketime { get; set; }
    // TabUsage
    public DateOnly GraphDate { get; set; } = 
        State.Current.Store.usage.Count > 0? State.Current.Store.usage.Last().Key: DateOnly.FromDateTime(DateTime.Today);
    public bool GraphEmpty { get => UsageGraph.Count() == 0; }
    public float GraphMax { get => UsageGraph.Count > 0? UsageGraph.First().usedTime: 1; }
    public bool GraphNextAvailable {
        get => State.Current.Store.usage.Count > 0 && State.Current.Store.usage.Last().Key != GraphDate;
    }
    public bool GraphPrevAvailable {
        get => State.Current.Store.usage.Count > 0 && State.Current.Store.usage.First().Key != GraphDate;
    }
    public List<UsageData> UsageGraph { get {
        if(!State.Current.Store.usage.TryGetValue(GraphDate, out UsageDate? value)) return [];
        var list = value.Entries.Select(kvp =>
        {
            return new UsageData
            {
                position = 0,
                exeName = kvp.Key,
                windowNames = string.Join('\n', kvp.Value.names),
                usedTime = kvp.Value.usedTime,
                icon = State.Current.IconRepo.TryGetValue(kvp.Key, out Bitmap? value) ? value : null
            };
        }).ToList();
        list.Sort((a,b) => a.usedTime < b.usedTime? 1: a.usedTime == b.usedTime? 0: -1);
        uint counter = 0;
        list.ForEach((i) => i.position = counter++);
        return list;
    } }
    public string DateUsage_Formatted { get =>
        "Total usage: " + (
            State.Current.Store.usage.TryGetValue(GraphDate, out var value) && value.totalUsage is not null ?
                TimeSpan.FromSeconds((int)value.totalUsage).ToString(@"hh\:mm") :
                "Unknown"
            );
    }
    // TabSync
    public bool Online { get => State.Current.Store.Online; }
    public Bitmap? QRCode { get; set; }
    // TabSystem
    public bool AutoStart { get => OS.Current.AutoStart; }
    public enum Tab
    {
        TabLimits, TabUsage, TabSync, TabSystem, TabQR
    }

    public ControlPanel() : this(null)
    {
        
    }
    /// <summary>
    /// Create an instance of ControlPanel where only one tab is available
    /// </summary>
    /// <param name="soleTab">The name of the tab which will be the only tab available</param>
    public ControlPanel(Tab? soleTab)
    {
        SetFields();
        InitializeComponent();
        DataContext = this;

        // Events
        State.Current.Changed += OnChanged;
        
        if(soleTab is Tab SoleTab)
        {
            TabLimits.IsEnabled = false;
            TabUsage.IsEnabled = false;
            TabSync.IsEnabled = false;
            TabSystem.IsEnabled = false;
            var tab = this.FindControl<TabItem>(SoleTab.ToString())
                      ?? throw new Exception("soleTab must exist");
            tabControl.SelectedItem = tab;
            tab.IsEnabled = true;
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
        UsedTime = State.Current.Store.usedTime;
        UsedTime_Formatted = string.Format("{0:D2}:{1:D2}.{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
        TodayLimit = State.Current.Store.todayTimeLimit == -1 ? -1 : State.Current.Store.todayTimeLimit / 60;
        MaxTime = TodayLimit == -1? 60*60*24: TodayLimit * 60;
        ExceededTime_Colour = new SolidColorBrush(UsedTime > MaxTime? Color.Parse("#df4d28") : Color.Parse("#9296d2"));
        DailyLimit = State.Current.Store.dailyTimeLimit == -1 ? -1 : State.Current.Store.dailyTimeLimit / 60;
        Bedtime = State.Current.Store.bedtime.ToTimeSpan();
        Waketime = State.Current.Store.waketime.ToTimeSpan();
    }

    // TabUsage
    private async void PrevDayUsage_Click(object? sender, RoutedEventArgs e)
    {
        GraphDate = State.Current.Store.usage.Keys.TakeWhile(k => k != GraphDate).LastOrDefault();
        var content = TabUsage.Content;
        TabUsage.Content = null;
        TabUsage.Content = content;
    }
    private async void NextDayUsage_Click(object? sender, RoutedEventArgs e)
    {
        GraphDate = State.Current.Store.usage.Keys.Reverse().TakeWhile(k => k != GraphDate).LastOrDefault();
        var content = TabUsage.Content;
        TabUsage.Content = null;
        TabUsage.Content = content;
    }

    // TabSync
    private async void AuthButton_Click(object? sender, RoutedEventArgs e)
    {
        if(!State.Current.Store.Online)
        {
            var result = await API.Current.Sync();
            if(!result)
            {
                var alert = new AlertDialog(
                    "Failed to sync! Check your network connection.",
                    "AutoLogout Sync"
                );
                await alert.ShowDialog(this);
                return;
            }
            State.Current.Store.Online = true;
            API.Current.syncTimer.Start();
            DeauthButton.IsEnabled = true;
            await OS.Current.SaveState();
        }

        // First sync complete, users should now be able to find this account on the Manager app
        string qrContent = $"https://autologout.yiays.com/app/addAccount?uuid={State.Current.Store.uuid}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        var memStream = new MemoryStream(qrCode.GetGraphic(1));

        QRCode = new Bitmap(memStream);
        ImageQRCode.Source = QRCode;
        InvalidateVisual();

        // Switch to the hidden tab
        tabControl.SelectedItem = TabQR;
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
            var result = await API.Current.Deauth();
            if(result)
            {
                State.Current.Store.Online = false;
                await OS.Current.SaveState();
                DeauthButton.IsEnabled = false;
                
                var alert2 = new AlertDialog(
                    "All devices which control this account have been signed out and all online data has been deleted.",
                    "AutoLogout Sync"
                );
                await alert2.ShowDialog(this);
                return;
            }
            var alert = new AlertDialog(
                "There was an error signing all devices out, please try again later.",
                "AutoLogout Sync"
            );
            await alert.ShowDialog(this);
        }
    }

    // TabSystem
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
    public async Task<bool> SetAutoStart(bool enable, Window? owner = null)
    {
        owner ??= this;
        var confirm = new ConfirmDialog(
            enable?
                "AutoLogout will start automatically on any user accounts that have set up a parent password. Continue?"
            :
                "This will prevent AutoLogout from auto-starting on all user accounts on this machine. Continue?",
            "Start automatically on login"
        );
        await confirm.ShowDialog(owner);
        if(confirm.Result ?? false)
        {
            return await OS.Current.RelaunchAsAdmin(enable? "--register": "--unregister");
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

    // Main view
    private async void ChangePassword_Click(object? sender, RoutedEventArgs e)
    {
        await ChangePassword();
    }
    public async Task<bool> ChangePassword(Window? owner = null)
    {
        owner ??= this;
        var prompt = new PromptDialog("Enter a new parent password.", "Change parent password", true);
        await prompt.ShowDialog(owner);
        var result = prompt.Result;
        if(result is not null)
        {
            if(result.Length > 0)
            {
                await State.Current.NewPassword(result);
                var alert = new AlertDialog("Parent password updated!", "Success");
                await alert.ShowDialog(owner);
                return true;
            }
            else
            {
                var alert = new AlertDialog("Parent password must not be empty!", "Error");
                await alert.ShowDialog(owner);
            }
        }
        return false;
    }
    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        State.Current.AcceptDelta(new DeltaState
        {
            todayTimeLimit = TodayLimit == -1 ? -1 : TodayLimit * 60,
            dailyTimeLimit = DailyLimit == -1 ? -1 : DailyLimit * 60,
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