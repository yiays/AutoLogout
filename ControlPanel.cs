
using System.Runtime.InteropServices.Swift;

namespace AutoLogout
{
  public partial class ControlPanel : Form
  {
    private readonly CountdownTimer parent;
    public ControlPanel(CountdownTimer parent)
    {
      this.parent = parent;
      Text = "AutoLogout Settings";
      FormBorderStyle = FormBorderStyle.FixedDialog;
      ShowInTaskbar = true;
      StartPosition = FormStartPosition.CenterScreen;
      AutoScaleMode = AutoScaleMode.Dpi;
      AutoScaleDimensions = new SizeF(125F, 125F);
      MinimizeBox = false;
      MaximizeBox = false;
      BackColor = Color.White;
      Width = 440;
      Height = 300;

      TableLayoutPanel table = new()
      {
        Dock = DockStyle.Top,
        AutoSize = true,
        ColumnCount = 2,
        RowCount = 2,
        Padding = new Padding(10),
      };

      Label dailylimitLabel = new()
      {
        Dock = DockStyle.Fill,
        Text = "Daily time limit (minutes):",
        AutoSize = true,
      };

      NumericUpDown dailylimitPicker = new()
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

      NumericUpDown todaylimitPicker = new()
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
      DateTimePicker waketimePicker = new()
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
      DateTimePicker sleeptimePicker = new()
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

      Controls.Add(table);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
      base.OnFormClosed(e);
      parent.controlPanel = null;
    }
  }
}