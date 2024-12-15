using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AutoLogout
{
    public partial class CountdownTimer : Form
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        private readonly Label textTimer;
        private readonly Button pauseButton;
        private readonly Button logoffButton;
        private readonly Button shutdownButton;
        private readonly System.Windows.Forms.Timer timer;
        private int remainingTime = 7200;
        private const int bedtimeH = 23;
        private const int bedtimeM = 0;
        private readonly LockoutWindow lockoutWindow;
        private readonly NotifyIcon notifyIcon;

        public CountdownTimer() {
            Text = "Time limit";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Width = 200;
            Height = 130;
            ControlBox = false; // No control buttons

            // Calculate the position just offset from the bottom right corner
            int offset = 0; // Adjust the offset as needed
            if(Screen.PrimaryScreen != null) {
                Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(workingArea.Right - Width - offset, workingArea.Bottom - Height - offset);
            }

            Load += OnLoad;

            timer = new System.Windows.Forms.Timer
            {
                Interval = 1000
            };
            timer.Tick += Timer_Tick;

            textTimer = new Label
            {
                Location = new Point(0, -10),
                AutoSize = true,
                Font = new Font("Segoe UI", 24, FontStyle.Bold)
            };
            EnforceBedtime();
            UpdateClock();

            pauseButton = new Button
            {
                Location = new Point(10, 44),
                Size = new Size(70, 28),
                Text = "Pause"
            };
            pauseButton.Click += Pause;

            IntPtr logofficonHandle = ExtractIcon(IntPtr.Zero, "shell32.dll", 44);
            Icon logoffIcon = Icon.FromHandle(logofficonHandle);
            Bitmap logoffBitmap = new Bitmap(logoffIcon.ToBitmap(), new Size(24, 24));
            logoffButton = new Button {
                Location = new Point(84, 44),
                Size = new Size(32, 28),
                Image = logoffBitmap,
                AccessibleDescription = "Log off"
            };
            logoffButton.Click += LogOff;
            ToolTip logoffHint = new ToolTip();
            logoffHint.SetToolTip(logoffButton, "Log off");

            IntPtr shutdowniconHandle = ExtractIcon(IntPtr.Zero, "shell32.dll", 27);
            Icon shutdownIcon = Icon.FromHandle(shutdowniconHandle);
            Bitmap shutdownBitmap = new Bitmap(shutdownIcon.ToBitmap(), new Size(24, 24));
            shutdownButton = new Button {
                Location = new Point(120, 44),
                Size = new Size(32, 28),
                Image = shutdownBitmap,
                AccessibleDescription = "Shutdown"
            };
            shutdownButton.Click += ShutDown;
            ToolTip shutdownHint = new ToolTip();
            shutdownHint.SetToolTip(shutdownButton, "Shut down");

            Controls.Add(textTimer);
            Controls.Add(pauseButton);
            Controls.Add(logoffButton);
            Controls.Add(shutdownButton);

            lockoutWindow = new LockoutWindow(this);

            notifyIcon = new NotifyIcon() {
                Icon = new Icon("Resources/icon.ico"),
                Visible = true,
                Text = "Show time limit"
            };
            notifyIcon.Click += FocusWindow;
        }

        private void OnLoad(object? sender, EventArgs e) {
            timer.Start();
        }

        private void Pause(object? sender, EventArgs e) {
            // Pause the timer
            if(timer.Enabled) {
                timer.Stop();
                pauseButton.Text = "Resume";
                lockoutWindow.Show();
            } else {
                lockoutWindow.Hide();
                TopMost = false;
                timer.Start();
                pauseButton.Text = "Pause";
                remainingTime--;
                EnforceBedtime();
                UpdateClock();
            }
        }

        private void Timer_Tick(object? sender, EventArgs e) {
            if(remainingTime > 0) {
                remainingTime--;
                EnforceBedtime();
                UpdateClock();
            } else {
                timer.Stop();
                if(CheckBedtime() <= 10)
                    ShutDown(null, null);
                else
                    LogOff(null, null);
            }
        }

        private void UpdateClock() {
            TimeSpan timeSpan = TimeSpan.FromSeconds(remainingTime);
            string timeString = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
            textTimer.Text = timeString;
        }

        private int CheckBedtime() {
            DateTime now = DateTime.Now;
            DateTime ninePM = new DateTime(now.Year, now.Month, now.Day, bedtimeH, bedtimeM, 0);

            TimeSpan difference = ninePM - now;
            return (int)difference.TotalSeconds;
        }

        private void EnforceBedtime() {
            int differenceInSeconds = CheckBedtime();

            if(differenceInSeconds < 0) {
                Task.Run(() => {
                    MessageBox.Show("It's past bedtime! Shutting down in 30 seconds.");
                    Invoke(() => FocusWindow(null, null));
                });
                remainingTime = 30;
            } else if(differenceInSeconds < remainingTime) {
                if(Math.Abs(remainingTime - differenceInSeconds) > 60) {
                    // Only alert if the difference is more than a minute
                    Task.Run(() => {
                        MessageBox.Show("Your time has been shortened so it will end with bedtime.");
                        Invoke(() => FocusWindow(null, null));
                    });
                }
                remainingTime = differenceInSeconds;
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
            Process.Start("shutdown", "/l /f");
            remainingTime = 0;
            Application.Exit();
        }

        private void ShutDown(object? sender, EventArgs? e) {
            Process.Start("shutdown", "/s /f");
            remainingTime = 0;
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if(remainingTime > 0)
                e.Cancel = true;
        }
    }
}
