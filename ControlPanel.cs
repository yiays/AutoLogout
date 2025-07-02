using System.Diagnostics;
using QRCoder;

namespace AutoLogout
{
  public partial class ControlPanel : Form
  {
    private readonly CountdownTimer parent;
    private readonly Label usedTimeIndicator;
    private readonly NumericUpDown dailylimitPicker;
    private readonly NumericUpDown todaylimitPicker;
    private readonly DateTimePicker waketimePicker;
    private readonly DateTimePicker sleeptimePicker;
    private Button DeauthButton;
    public ControlPanel(CountdownTimer parent)
    {
      this.parent = parent;
      Text = "AutoLogout Settings";
      Icon = new Icon("Resources/icon-light.ico");
      FormBorderStyle = FormBorderStyle.FixedDialog;
      ShowInTaskbar = true;
      StartPosition = FormStartPosition.CenterScreen;
      MinimizeBox = false;
      MaximizeBox = false;
      BackColor = Color.White;
      Width = 360;
      Height = 300;

      AutoScaleMode = AutoScaleMode.Dpi;
      AutoScaleDimensions = new(96F, 96F);

      TableLayoutPanel mainTable = new()
      {
        Dock = DockStyle.Top,
        AutoSize = true,
        ColumnCount = 1,
        RowCount = 2,
      };
      TableLayoutPanel table = new()
      {
        AutoSize = true,
        ColumnCount = 2,
        RowCount = 2,
        Padding = new Padding(10),
      };
      FlowLayoutPanel optionsPanel = new()
      {
        AutoSize = true,
      };
      FlowLayoutPanel buttonPanel = new()
      {
        Dock = DockStyle.Bottom,
        AutoSize = true,
        BackColor = SystemColors.Control,
        Padding = new Padding(8),
      };

      Label usedTimeLabel = new()
      {
        Dock = DockStyle.Fill,
        Text = "Time used today:",
        AutoSize = true,
      };

      usedTimeIndicator = new()
      {
        Dock = DockStyle.Fill,
        Text = "Loading...",
        AutoSize = true,
      };

      Label dailylimitLabel = new()
      {
        Dock = DockStyle.Fill,
        Text = "Daily time limit (minutes):",
        AutoSize = true,
      };

      dailylimitPicker = new()
      {
        Minimum = -1,
        Maximum = 1440, // 24 hours in minutes
      };

      Label todaylimitLabel = new()
      {
        Dock = DockStyle.Fill,
        Text = "Today's time limit (minutes):",
        AutoSize = true,
      };

      todaylimitPicker = new()
      {
        Minimum = -1,
        Maximum = 1440, // 24 hours in minutes
      };

      Label waketimeLabel = new()
      {
        Text = "Wake time:",
        AutoSize = true,
      };

      waketimePicker = new()
      {
        Format = DateTimePickerFormat.Time,
        ShowUpDown = true,
        Width = 150,
      };

      Label sleeptimeLabel = new()
      {
        Text = "Sleep time:",
        AutoSize = true,
      };

      sleeptimePicker = new()
      {
        Format = DateTimePickerFormat.Time,
        ShowUpDown = true,
        Width = 150,
      };

      table.Controls.Add(usedTimeLabel);
      table.Controls.Add(usedTimeIndicator);
      table.Controls.Add(dailylimitLabel);
      table.Controls.Add(dailylimitPicker);
      table.Controls.Add(todaylimitLabel);
      table.Controls.Add(todaylimitPicker);
      table.Controls.Add(waketimeLabel);
      table.Controls.Add(waketimePicker);
      table.Controls.Add(sleeptimeLabel);
      table.Controls.Add(sleeptimePicker);

      Button AuthButton = new() { Text = "Connect to your phone", AutoSize = true };
      AuthButton.Click += AuthButton_Click;
      DeauthButton = new() { Text = "Sign out all devices", AutoSize = true };
      DeauthButton.Click += DeauthButton_Click;
      Button ChangePasswordButton = new() { Text = "Change password", AutoSize = true };
      ChangePasswordButton.Click += ChangePasswordButton_Click;
      Button SaveButton = new() { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
      Button CancelButton = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
      SaveButton.Click += SaveButton_Click;

      if (Debugger.IsAttached)
      {
        optionsPanel.Controls.Add(AuthButton);
        optionsPanel.Controls.Add(DeauthButton);
      }
      optionsPanel.Controls.Add(ChangePasswordButton);
      buttonPanel.Controls.Add(SaveButton);
      buttonPanel.Controls.Add(CancelButton);

      mainTable.Controls.Add(table);
      mainTable.Controls.Add(optionsPanel);

      Controls.Add(mainTable);
      Controls.Add(buttonPanel);

      AcceptButton = SaveButton;
      base.CancelButton = CancelButton;

      // Initialize controls with current state
      OnStateChanged();
      // Subscribe to future state changes
      parent.state.Changed += OnStateChanged;
    }

    private void OnStateChanged()
    {
      // Update all controls that are affected by the state

      // If the event came from another thread, send it to the correct thread
      if (InvokeRequired)
      {
        Invoke(OnStateChanged);
        return;
      }

      TimeSpan timeSpan = TimeSpan.FromSeconds(parent.state.usedTime);
      string timeString = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
      usedTimeIndicator.Text = timeString;

      dailylimitPicker.Value = parent.state.dailyTimeLimit >= 0 ? parent.state.dailyTimeLimit / 60 : -1;

      todaylimitPicker.Value = parent.state.todayTimeLimit >= 0 ? parent.state.todayTimeLimit / 60 : -1;

      TimeOnly waketime = parent.state.waketime;
      DateTime wakeDateTime = DateTime.Today.Add(waketime.ToTimeSpan());
      waketimePicker.Value = wakeDateTime;

      TimeOnly bedtime = parent.state.bedtime;
      DateTime sleepDateTime = DateTime.Today.Add(bedtime.ToTimeSpan());
      sleeptimePicker.Value = sleepDateTime;

      DeauthButton.Enabled = parent.state.OnlineMode;
    }

    private void AuthButton_Click(object? sender, EventArgs e)
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

      // Show QR in a simple dialog
      Form qrForm = new Form
      {
        Text = "Scan this QR code with your phone",
        ClientSize = new Size(600, 600),
        FormBorderStyle = FormBorderStyle.SizableToolWindow,
        StartPosition = FormStartPosition.CenterParent
      };
      PictureBox pb = new PictureBox
      {
        Dock = DockStyle.Fill,
        Image = new Bitmap(qrBitmap),
        SizeMode = PictureBoxSizeMode.Zoom
      };
      qrForm.Controls.Add(pb);
      qrForm.ShowDialog(this);
    }
    private void DeauthButton_Click(object? sender, EventArgs e)
    {
      if (MessageBox.Show("Are you sure you want delete all your online data and sign out all devices?", "AutoLogout", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
      {
        Task.Run(parent.state.Deauth);
      }
    }
    private void ChangePasswordButton_Click(object? sender, EventArgs e)
    {
      if (parent.state.NewPassword())
        MessageBox.Show("Password changed successfully.", "AutoLogout", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    private void SaveButton_Click(object? sender, EventArgs e)
    {
      parent.state.dailyTimeLimit = (int)(dailylimitPicker.Value >= 0 ? dailylimitPicker.Value * 60 : -1);
      parent.state.todayTimeLimit = (int)(todaylimitPicker.Value >= 0 ? todaylimitPicker.Value * 60 : -1);
      // Give parents some time to correct their mistake if they set the time limit too low
      if (parent.state.remainingTime == 0)
        parent.state.todayTimeLimit = parent.state.usedTime + 30;
      parent.state.waketime = new(waketimePicker.Value.Hour, waketimePicker.Value.Minute);
      parent.state.bedtime = new(sleeptimePicker.Value.Hour, sleeptimePicker.Value.Minute);
      parent.state.SaveToRegistry();
      parent.state.TriggerStateChanged();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
      parent.state.Changed -= OnStateChanged;
      base.OnFormClosed(e);
      parent.controlPanel = null;
    }
  }
}