using System.Diagnostics;
using System.Media;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AutoLogout
{
 public partial class CountdownTimer : Form
  {
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    private readonly Panel mainPanel;
    private readonly FlowLayoutPanel buttonPanel;
    private readonly Label textTimer;
    private readonly Button pauseButton;
    private readonly Button logoffButton;
    private readonly Button shutdownButton;
    private readonly Button settingsButton;
    private readonly System.Windows.Forms.Timer timer;
    private readonly NotifyIcon notifyIcon;

    // Config defaults
    private String password = "";
    private int? dailyTimeLimit;
    private int? remainingTime;
    private DateOnly remainingTimeDay = new(1, 1, 1);
    private int? bedtimeH;
    private int? bedtimeM;
    private int? waketimeH;
    private int? waketimeM;
    private bool graceGiven = false;

    private readonly LockoutWindow lockoutWindow;
    private readonly ControlPanel controlPanel;
    private readonly AudioControl audioControl;

    public CountdownTimer() {
      Text = "Time limit";
      FormBorderStyle = FormBorderStyle.FixedToolWindow;
      ShowInTaskbar = false;
      StartPosition = FormStartPosition.Manual;
      Width = 206;
      Height = 130;
      ControlBox = false; // No control buttons
      AutoScaleMode = AutoScaleMode.Dpi;
      AutoScaleDimensions = new SizeF(125F, 125F);
      MaximizeBox = false;

      // Events
      Reposition(null, null);
      SystemEvents.DisplaySettingsChanged += Reposition;

      Load += OnLoad;

      timer = new System.Windows.Forms.Timer
      {
        Interval = 1000
      };
      timer.Tick += Timer_Tick;

      // Form elements
      mainPanel = new Panel
      {
        Dock = DockStyle.Top,
        AutoSize = true,
      };
      buttonPanel = new FlowLayoutPanel
      {
        Dock = DockStyle.Bottom,
        FlowDirection = FlowDirection.LeftToRight,
        AutoSize = true
      };

      textTimer = new Label
      {
        Location = new Point(0, -10),
        AutoSize = true,
        Font = new Font("Segoe UI", 22, FontStyle.Bold)
      };

      pauseButton = new Button
      {
        Text = "Pause",
        Width = 90,
        Height = 32,
      };
      pauseButton.Click += Pause;

      IntPtr logofficonHandle = ExtractIcon(IntPtr.Zero, "shell32.dll", 44);
      Icon logoffIcon = Icon.FromHandle(logofficonHandle);
      Bitmap logoffBitmap = new Bitmap(logoffIcon.ToBitmap(), new Size(24, 24));
      logoffButton = new Button {
        Image = logoffBitmap,
        AccessibleDescription = "Log off",
        Width = 32,
        Height = 32,
      };
      logoffButton.Click += LogOff;
      ToolTip logoffHint = new ToolTip();
      logoffHint.SetToolTip(logoffButton, "Log off");

      IntPtr shutdowniconHandle = ExtractIcon(IntPtr.Zero, "shell32.dll", 27);
      Icon shutdownIcon = Icon.FromHandle(shutdowniconHandle);
      Bitmap shutdownBitmap = new Bitmap(shutdownIcon.ToBitmap(), new Size(24, 24));
      shutdownButton = new Button {
        Image = shutdownBitmap,
        AccessibleDescription = "Shutdown",
        Width = 32,
        Height = 32,
      };
      shutdownButton.Click += ShutDown;
      ToolTip shutdownHint = new ToolTip();
      shutdownHint.SetToolTip(shutdownButton, "Shut down");

      IntPtr settingsiconHandle = ExtractIcon(IntPtr.Zero, "shell32.dll", 21);
      Icon settingsIcon = Icon.FromHandle(settingsiconHandle);
      Bitmap settingsBitmap = new Bitmap(settingsIcon.ToBitmap(), new Size(24, 24));
      settingsButton = new Button
      {
        Image = settingsBitmap,
        AccessibleDescription = "Settings",
        Width = 32,
        Height = 32,
      };
      settingsButton.Click += Settings;
      ToolTip settingsHint = new ToolTip();
      settingsHint.SetToolTip(settingsButton, "Settings");

      mainPanel.Controls.Add(textTimer);
      buttonPanel.Controls.Add(pauseButton);
      buttonPanel.Controls.Add(logoffButton);
      buttonPanel.Controls.Add(shutdownButton);
      buttonPanel.Controls.Add(settingsButton);

      Controls.Add(mainPanel);
      Controls.Add(buttonPanel);

      lockoutWindow = new LockoutWindow(this);
      controlPanel = new ControlPanel(this);
      audioControl = new AudioControl();

      notifyIcon = new NotifyIcon() {
        Icon = new Icon("Resources/icon.ico"),
        Visible = true,
        Text = "Show time limit"
      };
      notifyIcon.Click += FocusWindow;
    }

    private void LoadFromRegistry() {
      RegistryKey? key = Registry.CurrentUser.CreateSubKey("AutoLogout", true);
      if (key == null)
      {
        MessageBox.Show("Unable to access settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }

      // Load current app state from registry
      password = (string)key.GetValue("password", "");

      bedtimeH = (int?)key.GetValue("bedtimeH", null);
      if (bedtimeH == -1) bedtimeH = null;
      bedtimeM = (int?)key.GetValue("bedtimeM", null);
      if (bedtimeM == -1) bedtimeM = null;
      waketimeH = (int?)key.GetValue("waketimeH", null);
      if (waketimeH == -1) waketimeH = null;
      waketimeM = (int?)key.GetValue("waketimeM", null);
      if (waketimeM == -1) waketimeM = null;

      dailyTimeLimit = (int?)key.GetValue("dailyTimeLimit", null);
      if (dailyTimeLimit == -1) dailyTimeLimit = null;
      remainingTimeDay = DateOnly.Parse((String)key.GetValue("remainingTimeDay", "1/01/0001"));
      remainingTime = (int?)key.GetValue("remainingTime", null);
      if (remainingTime == -1) remainingTime = null;
    }

    private void SaveToRegistry() {
      RegistryKey? key = Registry.CurrentUser.CreateSubKey("AutoLogout");
      if (key != null)
      {
        key.SetValue("password", password);
        key.SetValue("remainingTimeDay", DateOnly.FromDateTime(DateTime.Now));
        key.SetValue("dailyTimeLimit", dailyTimeLimit ?? -1);
        key.SetValue("remainingTime", remainingTime ?? -1);
        key.SetValue("bedtimeH", bedtimeH ?? -1);
        key.SetValue("bedtimeM", bedtimeM ?? -1);
        key.SetValue("waketimeH", waketimeH ?? -1);
        key.SetValue("waketimeM", waketimeM ?? -1);
      }
    }

    private void Reposition(object? sender, EventArgs? e)
    {
      // Calculate the position just offset from the bottom right corner
      if (Screen.PrimaryScreen != null)
      {
        Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(workingArea.Right - Width, workingArea.Bottom - Height);
      }
    }

    private void OnLoad(object? sender, EventArgs e) {
      LoadFromRegistry();
      DateOnly currentDay = DateOnly.FromDateTime(DateTime.Now);
      if (remainingTimeDay != currentDay)
      {
        // If the day is different, reset the remaining time
        remainingTime = dailyTimeLimit;
        remainingTimeDay = currentDay;
      }
      UpdateClock();
      EnforceBedtime();
      timer.Start();
    }

    private void Pause(object? sender, EventArgs e) {
      // Pause the timer
      if (timer.Enabled)
      {
        timer.Stop();
        pauseButton.Text = "Resume";
        controlPanel.Hide();
        lockoutWindow.Show();
        audioControl.Mute(null, null);
        settingsButton.Enabled = false;
      }
      else
      {
        lockoutWindow.Hide();
        TopMost = false;
        timer.Start();
        pauseButton.Text = "Pause";
        remainingTime--;
        EnforceBedtime();
        UpdateClock();
        audioControl.Unmute();
        settingsButton.Enabled = true;
      }
    }
    
    private void Settings(object? sender, EventArgs e) {
      String password = Prompt.ShowDialog("Enter the parent password to continue.", "AutoLogout Settings");

      if (password == this.password)
        controlPanel.Show();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
      UpdateClock();
      if (remainingTime == null)
        return;
      if (remainingTime > 0)
      {
        remainingTime--;
        if (remainingTime % 10 == 0)
        {
          // Save to the registry every 10 seconds
          SaveToRegistry();
          Console.WriteLine("Writing to registry...");
        }
        EnforceBedtime();
        if (remainingTime == 600)
        {
          SoundPlayer player = new SoundPlayer("Resources/alarm.wav");
          player.Play();
          Task.Run(() =>
          {
            MessageBox.Show(
            "Your time is up in 10 minutes!", "Time limit warning", MessageBoxButtons.OK, MessageBoxIcon.Warning
            );
          });
        }
        if (remainingTime == 30 && !graceGiven)
        {
          Task.Run(() =>
          {
            MessageBox.Show(
            "Your time is up in 30 seconds!", "Time limit warning", MessageBoxButtons.OK, MessageBoxIcon.Warning
            );
          });
          graceGiven = true;
        }
      }
      else
      {
        timer.Stop();
        double? remainingTime = CheckBedtime();
        if (remainingTime != null && remainingTime <= 10)
          ShutDown(null, null);
        else
          LogOff(null, null);
      }
    }

    private void UpdateClock() {
      if (remainingTime == null)
      {
        textTimer.Text = "No limit";
        return;
      }
      TimeSpan timeSpan = TimeSpan.FromSeconds((long)remainingTime);
      string timeString = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
      textTimer.Text = timeString;
    }

    private double? CheckBedtime() {
      if (bedtimeH == null || bedtimeM == null || waketimeH == null || waketimeM == null)
        return null;
      DateTime now = DateTime.Now;
      // Create sleepTime and wakeTime, assuming both are for today
      DateTime sleepTime = new(now.Year, now.Month, now.Day, (int)bedtimeH, (int)bedtimeM, 0);
      if(bedtimeH < 12) sleepTime = sleepTime.AddDays(1);
      DateTime wakeTime = new(now.Year, now.Month, now.Day, (int)waketimeH, (int)waketimeM, 0);

      // Correct sleepTime and wakeTime based on the current time
      if(now > wakeTime) {
        // if sleepTime is before wakeTime, it will next occur tomorrow
        if(sleepTime < wakeTime) sleepTime = sleepTime.AddDays(1);
      }else{
        // sleepTime must be before wakeTime if wakeTime hasn't passed yet
        if(now < sleepTime) sleepTime = sleepTime.AddDays(-1);
      }
      return (sleepTime - now).TotalSeconds;
      }

    private void EnforceBedtime() {
      double? differenceInSeconds = CheckBedtime();
      
      if (differenceInSeconds == null) return;
      else if (differenceInSeconds < 0)
      {
        if (graceGiven) return;
        Task.Run(() =>
        {
          MessageBox.Show("It's past bedtime! Shutting down in 30 seconds.");
          Invoke(() => FocusWindow(null, null));
        });
        remainingTime = 30;
        graceGiven = true;
      }
      else if (remainingTime == null)
      {
        // No time limit, just bedtime
        remainingTime = (int)differenceInSeconds;
      }
      else if (differenceInSeconds < remainingTime)
      {
        if (Math.Abs((decimal)remainingTime - (decimal)differenceInSeconds) > 60)
        {
          // Only alert if the difference is more than a minute
          Task.Run(() =>
          {
            MessageBox.Show("Your time has been shortened so it will end with bedtime.");
            Invoke(() => FocusWindow(null, null));
          });
        }
        remainingTime = (int)differenceInSeconds;
      }
    }

    public void ReassertTopMost(object? sender, EventArgs? e) {
      TopMost = false;
      TopMost = true;
    }

    public void FocusWindow(object? sender, EventArgs? e) {
      Activate();
    }

    private void LogOff(object? sender, EventArgs? e) {
      SaveToRegistry();
      timer.Stop();
      pauseButton.Enabled = false;
      remainingTime = 0;
      if (Debugger.IsAttached)
      {
        MessageBox.Show("Log off");
        return;
      }
      Process.Start("shutdown", "/l /f");
      Application.Exit();
    }

    private void ShutDown(object? sender, EventArgs? e) {
      SaveToRegistry();
      timer.Stop();
      pauseButton.Enabled = false;
      remainingTime = 0;
      if (Debugger.IsAttached)
      {
        MessageBox.Show("Shut down");
        return;
      }
      Process.Start("shutdown", "/p /f");
      Application.Exit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e) {
      if(remainingTime == null || remainingTime > 0)
        e.Cancel = true;
      }
  }
}
