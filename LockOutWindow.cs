
namespace AutoLogout
{
    public partial class LockoutWindow : Form
    {
        private readonly Label instructions;

        public LockoutWindow(CountdownTimer parent) {
            Text = "Pause screen";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;

            instructions = new Label {
                Text = "Timer paused. Click anywhere to show the timer.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                TextAlign = ContentAlignment.MiddleCenter
            };
            instructions.Click += parent.ReassertTopMost;

            Controls.Add(instructions);

            Shown += OnShown;
            Click += parent.ReassertTopMost;
        }

        private void OnShown(object? sender, EventArgs? e){
            Location = new Point(0, 0);
            TopMost = false;
            TopMost = true;
            
            if(Screen.PrimaryScreen != null) {
                Width = Screen.PrimaryScreen.Bounds.Width;
                Height = Screen.PrimaryScreen.Bounds.Height;
            }

            instructions.Location = new Point(
                (Width - instructions.Width) / 2,
                (Height - instructions.Height) / 2
            );
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;  // Prevents window from closing
        }
    }
}
