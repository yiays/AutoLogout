namespace AutoLogout
{
  public partial class ControlPanel : Form
  {
    private readonly CountdownTimer parent;
    private readonly NumericUpDown dailylimitPicker;
    private readonly NumericUpDown todaylimitPicker;
    private readonly DateTimePicker waketimePicker;
    private readonly DateTimePicker sleeptimePicker;
    public ControlPanel(CountdownTimer parent)
    {
      this.parent = parent;
      Text = "AutoLogout Settings";
      FormBorderStyle = FormBorderStyle.FixedDialog;
      ShowInTaskbar = true;
      StartPosition = FormStartPosition.CenterScreen;
      MinimizeBox = false;
      MaximizeBox = false;
      BackColor = Color.White;
      Width = 360;
      Height = 250;

      AutoScaleMode = AutoScaleMode.Dpi;
      AutoScaleDimensions = new(96F, 96F);

      TableLayoutPanel table = new()
      {
        Dock = DockStyle.Top,
        AutoSize = true,
        ColumnCount = 2,
        RowCount = 2,
        Padding = new Padding(10),
      };
      FlowLayoutPanel buttonPanel = new()
      {
        Dock = DockStyle.Bottom,
        FlowDirection = FlowDirection.RightToLeft,
        AutoSize = true,
        BackColor = SystemColors.Control,
        Padding = new Padding(8),
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
        Maximum = 1440, // 24 hours in minutes,
        Value = parent.dailyTimeLimit >= 0 ? parent.dailyTimeLimit / 60 : -1,
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
        Maximum = 1440, // 24 hours in minutes,
        Value = parent.remainingTime >= 0 ? parent.remainingTime / 60 : -1,
      };

      Label waketimeLabel = new()
      {
        Text = "Wake time:",
        AutoSize = true,
      };

      TimeOnly waketime = parent.waketime;
      DateTime wakeDateTime = DateTime.Today.Add(waketime.ToTimeSpan());
      waketimePicker = new()
      {
        Format = DateTimePickerFormat.Time,
        ShowUpDown = true,
        Value = wakeDateTime,
        Width = 150,
      };

      Label sleeptimeLabel = new()
      {
        Text = "Sleep time:",
        AutoSize = true,
      };

      TimeOnly bedtime = parent.bedtime;
      DateTime sleepDateTime = DateTime.Today.Add(bedtime.ToTimeSpan());
      sleeptimePicker = new()
      {
        Format = DateTimePickerFormat.Time,
        ShowUpDown = true,
        Value = sleepDateTime,
        Width = 150,
      };

      table.Controls.Add(dailylimitLabel, 0, 0);
      table.Controls.Add(dailylimitPicker, 1, 0);
      table.Controls.Add(todaylimitLabel, 0, 1);
      table.Controls.Add(todaylimitPicker, 1, 1);
      table.Controls.Add(waketimeLabel, 0, 2);
      table.Controls.Add(waketimePicker, 1, 2);
      table.Controls.Add(sleeptimeLabel, 0, 3);
      table.Controls.Add(sleeptimePicker, 1, 3);

      Button save = new() { Text = "Save", Width = 100, Height = 32, DialogResult = DialogResult.OK };
      Button cancel = new() { Text = "Cancel", Width = 100, Height = 32, DialogResult = DialogResult.Cancel };
      save.Click += Save;

      buttonPanel.Controls.Add(cancel);
      buttonPanel.Controls.Add(save);

      Controls.Add(table);
      Controls.Add(buttonPanel);
    }

    private void Save(object? sender, EventArgs e)
    {
      parent.dailyTimeLimit = (int)(dailylimitPicker.Value >= 0 ? dailylimitPicker.Value * 60 : -1);
      parent.remainingTime = (int)(todaylimitPicker.Value >= 0 ? todaylimitPicker.Value * 60 : -1);
      parent.waketime = new(waketimePicker.Value.Hour, waketimePicker.Value.Minute);
      parent.bedtime = new(sleeptimePicker.Value.Hour, sleeptimePicker.Value.Minute);
      parent.SaveToRegistry();
      parent.UpdateClock();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
      base.OnFormClosed(e);
      parent.controlPanel = null;
    }
  }
}