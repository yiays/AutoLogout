namespace AutoLogout
{
  public partial class FirstTime : Form
  {
    public FirstTime()
    {
      Text = "AutoLogout Setup";
      Icon = new Icon("Resources/icon-light.ico");
      FormBorderStyle = FormBorderStyle.FixedDialog;
      ShowInTaskbar = true;
      StartPosition = FormStartPosition.CenterScreen;
      MinimizeBox = false;
      MaximizeBox = false;
      BackColor = Color.White;
      Width = 512;
      Height = 480;

      AutoScaleMode = AutoScaleMode.Dpi;
      AutoScaleDimensions = new(96F, 96F);

      int pageWidth = Width - 28;

      TableLayoutPanel mainTable = new()
      {
        Dock = DockStyle.Top,
        AutoSize = true,
        ColumnCount = 1,
        RowCount = 2,
      };
      FlowLayoutPanel buttonPanel = new()
      {
        Dock = DockStyle.Bottom,
        AutoSize = true,
        BackColor = SystemColors.Control,
        Padding = new Padding(8),
      };

      PictureBox pictureBox = new()
      {
        Image = Image.FromFile(@"Resources\feature-graphic-desktop.jpg"),
        SizeMode = PictureBoxSizeMode.Zoom,
        Width = pageWidth,
        Height = (int)(pageWidth * 0.3)
      };

      Label introLabel = new()
      {
        Text = "Welcome to AutoLogout!\n\nIt appears that this is the first time you have used " +
          "AutoLogout. I can set up automatic start for you. Though there are some things you " +
          "should note;",
        Margin = new Padding(Top = 4),
        AutoSize = true,
        MaximumSize = new Size { Width = pageWidth },
      };
      Label disclaimerLabel = new()
      {
        Text = " - Toggling automatic start requires an administrator account.\n" +
          " - AutoLogout should be in a folder that all accounts can see, but only admins can change.\n" +
          "   - If you used the installer, this is already set.\n" +
          " - Once automatic start is enabled, you shouldn't move or delete AutoLogout.\n" +
          " - You can configure this later in the ControlPanel.",
        Font = new Font(introLabel.Font, FontStyle.Italic),
        Margin = new Padding(Top = 4),
        AutoSize = true,
        MaximumSize = new Size { Width = pageWidth },
      };

      mainTable.Controls.Add(pictureBox);
      mainTable.Controls.Add(introLabel);
      mainTable.Controls.Add(disclaimerLabel);

      Button ContinueButton = new() { Text = "Continue", AutoSize = true };
      Button CancelButton = new() { Text = "Cancel", AutoSize = true };
      ContinueButton.Click += ContinueButton_Click;
      CancelButton.Click += CancelButton_Click;

      buttonPanel.Controls.Add(ContinueButton);
      buttonPanel.Controls.Add(CancelButton);

      Controls.Add(mainTable);
      Controls.Add(buttonPanel);
    }

    private void ContinueButton_Click(object? sender, EventArgs e)
    {
      Common.RelaunchAsAdmin("--register");
      Close();
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
      Close();
    }
    
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
      base.OnFormClosed(e);
      Common.Relaunch("");
    }
  }
}