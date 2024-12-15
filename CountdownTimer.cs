using System.Diagnostics;

namespace AutoLogout
{
    public partial class CountdownTimer : Form
    {
        private readonly Label textTimer;
        private readonly Button pauseButton;
        private readonly System.Windows.Forms.Timer timer;
        private int remainingTime = 7200;
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
                AutoSize = true,
                Text = "Pause"
            };
            pauseButton.Click += Pause;

            Controls.Add(textTimer);
            Controls.Add(pauseButton);

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


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if(remainingTime > 0)
                e.Cancel = true;
        }
    }
}
