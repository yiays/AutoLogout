namespace AutoLogout
{
    public partial class LockoutWindow : Form
    {
        private readonly Label instructions;

        public LockoutWindow() {
            Text = "Pause screen";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(0, 0);
            BackColor = Color.Black;
            TopMost = true;
            
            if(Screen.PrimaryScreen != null) {
                Width = Screen.PrimaryScreen.Bounds.Width;
                Height = Screen.PrimaryScreen.Bounds.Height;
            }

            instructions = new Label {
                Text = "Timer paused. If you can't see the timer, click here.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                TextAlign = ContentAlignment.MiddleCenter
            };

            instructions.Location = new Point(
                Width / 2 - instructions.Width,
                Height / 2 - instructions.Height
            );

            Controls.Add(instructions);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;  // Prevents window from closing
        }
    }
}
