
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
      FormBorderStyle = FormBorderStyle.FixedToolWindow;
      ShowInTaskbar = true;
      StartPosition = FormStartPosition.CenterScreen;
      AutoScaleMode = AutoScaleMode.Dpi;
      AutoScaleDimensions = new SizeF(125F, 125F);
      MaximizeBox = false;
      Width = 400;
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
        Text = "Daily time limit:",
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
        Text = "Today's time limit:",
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