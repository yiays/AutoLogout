
using Microsoft.Win32;

namespace AutoLogout
{
  public static class State
  {
    // Config defaults
    public static string hashedPassword = "";
    public static int dailyTimeLimit;
    public static int remainingTime;
    public static int usedTime = 0;
    public static DateOnly remainingTimeDay = new(1, 1, 1);
    public static TimeOnly bedtime = new(0, 0);
    public static TimeOnly waketime = new(0, 0);
    public static bool graceGiven = false;

    public static int LoadFromRegistry()
    {
      RegistryKey? key = Registry.CurrentUser.CreateSubKey("Software\\Yiays\\AutoLogout", true);
      if (key == null)
      {
        MessageBox.Show("Unable to access settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        remainingTime = 0;
        return -1;
      }

      // Load current app state from registry
      hashedPassword = (string)key.GetValue("password", "");

      string bedtimeRaw = (string)key.GetValue("bedtime", "0:00");
      bedtime = TimeOnly.Parse(bedtimeRaw);
      string waketimeRaw = (string)key.GetValue("waketime", "0:00");
      waketime = TimeOnly.Parse(waketimeRaw);

      dailyTimeLimit = (int)key.GetValue("dailyTimeLimit", -1);
      remainingTimeDay = DateOnly.Parse((String)key.GetValue("remainingTimeDay", "1/01/0001"));
      remainingTime = (int)key.GetValue("remainingTime", -1);
      usedTime = (int)key.GetValue("usedTime", 0);

      return 0;
    }

    public static int SaveToRegistry()
    {
      RegistryKey? key = Registry.CurrentUser.CreateSubKey("Software\\Yiays\\AutoLogout");
      if (key == null)
      {
        MessageBox.Show("Unable to save settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        remainingTime = 0;
        return -1;
      }

      key.SetValue("password", hashedPassword);
      key.SetValue("remainingTimeDay", DateOnly.FromDateTime(DateTime.Today));
      key.SetValue("dailyTimeLimit", dailyTimeLimit);
      key.SetValue("remainingTime", remainingTime);
      key.SetValue("usedTime", usedTime);
      key.SetValue("bedtime", bedtime.ToString());
      key.SetValue("waketime", waketime.ToString());

      return 0;
    }
  }

  public static class Prompt
  {
    private partial class PromptForm : Form
    {
      public TextBox textBox;
      public PromptForm(string text, string caption, bool sensitive = false)
      {
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Text = caption;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        BackColor = Color.White;
        Width = 350;
        Height = 200;

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new(96F, 96F);

        FlowLayoutPanel mainPanel = new()
        {
          Dock = DockStyle.Top,
          FlowDirection = FlowDirection.TopDown,
          Padding = new Padding(12),
        };
        FlowLayoutPanel buttonPanel = new()
        {
          Dock = DockStyle.Bottom,
          FlowDirection = FlowDirection.RightToLeft,
          AutoSize = true,
          BackColor = SystemColors.Control,
          Padding = new Padding(8),
        };


        Label textLabel = new() { MaximumSize = new Size(576, 100), AutoSize = true, Text = text, Padding = new() { Bottom = 10 } };
        textBox = new() { Width = 300 };
        if (sensitive) textBox.PasswordChar = '*';

        Button confirmation = new() { Text = "Ok", Width = 100, Height = 32, DialogResult = DialogResult.OK };
        Button cancel = new() { Text = "Cancel", Width = 100, Height = 32, DialogResult = DialogResult.Cancel };
        confirmation.Click += (sender, e) => { Close(); };

        mainPanel.Controls.Add(textLabel);
        mainPanel.Controls.Add(textBox);

        buttonPanel.Controls.Add(cancel);
        buttonPanel.Controls.Add(confirmation);

        Controls.Add(mainPanel);
        Controls.Add(buttonPanel);

        AcceptButton = confirmation;
        CancelButton = cancel;
      }
    }

    public static string? ShowDialog(string text, string caption, bool sensitive = false)
    {
      PromptForm prompt = new(text, caption, sensitive);

      return prompt.ShowDialog() == DialogResult.OK ? prompt.textBox.Text : null;
    }
  }
}