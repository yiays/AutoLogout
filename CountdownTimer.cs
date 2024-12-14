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

        public CountdownTimer() {
            Text = "Time limit";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Width = 200;
            Height = 130;

            ControlBox = false;

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
            timer.Tick += TimerTick;

            textTimer = new Label
            {
                Location = new Point(0, -10),
                AutoSize = true,
                Font = new Font("Segoe UI", 24, FontStyle.Bold)
            };
            CheckBedtime();
            UpdateClock();

            pauseButton = new Button
            {
                Location = new Point(10, 44),
                AutoSize = true,
                Text = "Pause"
            };
            pauseButton.Click += pauseButton_Click;

            Controls.Add(textTimer);
            Controls.Add(pauseButton);

            lockoutWindow = new LockoutWindow();
            lockoutWindow.Click += ReassertTopMost;
        }

        private void OnLoad(object? sender, EventArgs e) {
            TopMost = true;
            timer.Start();
        }

        private void pauseButton_Click(object? sender, EventArgs e) {
            // Pause the timer
            if(timer.Enabled) {
                timer.Stop();
                pauseButton.Text = "Resume";
                lockoutWindow.Show();
                ReassertTopMost(null, null);
            } else {
                lockoutWindow.Hide();
                timer.Start();
                pauseButton.Text = "Pause";
                remainingTime--;
                CheckBedtime();
                UpdateClock();
            }
        }

        private void TimerTick(object? sender, EventArgs e) {
            if(remainingTime > 0) {
                remainingTime--;
                UpdateClock();
            } else {
                timer.Stop();
                
                ProcessStartInfo startInfo = new()
                {
                    FileName = "notepad.exe",
                    UseShellExecute = false
                };
                Process.Start(startInfo);
                Application.Exit();
            }
        }

        private void UpdateClock() {
            TimeSpan timeSpan = TimeSpan.FromSeconds(remainingTime);
            string timeString = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
            textTimer.Text = timeString;
        }

        private void CheckBedtime() {
            DateTime now = DateTime.Now;
            DateTime ninePM = new DateTime(now.Year, now.Month, now.Day, 21, 0, 0);

            if (now > ninePM)
            {
                ninePM = ninePM.AddDays(1);  // Move to next day's 9 PM if it's already past 9 PM today
            }

            TimeSpan difference = ninePM - now;
            int differenceInSeconds = (int)difference.TotalSeconds;

            if(differenceInSeconds < remainingTime) {
                Task.Run(() => {
                    MessageBox.Show("Your time has been shortened so that it will end with bedtime.");
                    Invoke(() => ReassertTopMost(null, null));
                });
                if(differenceInSeconds > 0)
                    remainingTime = differenceInSeconds;
                else remainingTime = 10;
            }
        }

        private void ReassertTopMost(object? sender, EventArgs? e) {
            TopMost = false;
            TopMost = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if(remainingTime > 0)
                e.Cancel = true;
        }
    }
}
