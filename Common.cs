using System.Diagnostics;
using Microsoft.Win32;
using BC = BCrypt.Net;

namespace AutoLogout
{
  public class State
  {
    private static string REGKEY { get => Debugger.IsAttached ? "Software\\Yiays\\AutoLogout-Preview" : "Software\\Yiays\\AutoLogout"; }
    public bool OnlineMode = false;
    public bool ExitIntent = false;
    public bool Paused = false;
    public Guid authKey = Guid.Empty;
    public Guid uuid = Guid.Empty;
    public string hashedPassword = "";
    public int dailyTimeLimit = -1;
    public int todayTimeLimit = -1;
    public int tempTimeLimit = -1; // This stores temporary overrides to the time limit. Takes priority over bedtime
    public int bedtimeTimeLimit = -1; // This stores bedtime-related overrides to the time limit
    private int realTimeLimit { get => tempTimeLimit != -1? tempTimeLimit: bedtimeTimeLimit != -1 ? bedtimeTimeLimit : todayTimeLimit; }
    public int remainingTime { get => realTimeLimit == -1 ? -1 : Math.Max(realTimeLimit - usedTime, 0); }
    public int usedTime = 0;
    public DateOnly usageDate = DateOnly.FromDateTime(DateTime.Today);
    public TimeOnly bedtime = new TimeOnly(0, 0);
    public TimeOnly waketime = new TimeOnly(0, 0);
    public bool graceGiven = false;
    public Guid? syncAuthor = null;

    public event Action? Changed;

    public API api = new();

    public void NewGuid()
    {
      uuid = Guid.NewGuid();
    }

    public void TriggerStateChanged()
    {
      Changed?.Invoke();
    }

    public int FromRegistry()
    {
      RegistryKey? key = Registry.CurrentUser.CreateSubKey(REGKEY, true);
      if (key == null)
      {
        MessageBox.Show("Unable to access settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return -1;
      }

      // Load current app state from registry
      OnlineMode = bool.Parse((string)key.GetValue("OnlineMode", "false"));

      string? rawAuthKey = (string?)key.GetValue("authKey", null);
      authKey = rawAuthKey is null ? Guid.Empty : new Guid(rawAuthKey);

      string? rawGuid = (string?)key.GetValue("guid", null);
      uuid = rawGuid is null ? Guid.Empty : new Guid(rawGuid);

      hashedPassword = (string)key.GetValue("password", "");

      string bedtimeRaw = (string)key.GetValue("bedtime", "0:00");
      bedtime = TimeOnly.Parse(bedtimeRaw);
      string waketimeRaw = (string)key.GetValue("waketime", "0:00");
      waketime = TimeOnly.Parse(waketimeRaw);

      dailyTimeLimit = (int)key.GetValue("dailyTimeLimit", -1);
      usageDate = DateOnly.Parse((string)key.GetValue("usageDate", "1/01/0001"));
      todayTimeLimit = (int)key.GetValue("todayTimeLimit", -1);
      usedTime = (int)key.GetValue("usedTime", 0);

      return 0;
    }

    public int SaveToRegistry()
    {
      RegistryKey? key = Registry.CurrentUser.CreateSubKey(REGKEY);
      if (key == null)
      {
        MessageBox.Show("Unable to save settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        ExitIntent = true;
        return -1;
      }

      key.SetValue("OnlineMode", OnlineMode);
      key.SetValue("authKey", authKey);
      key.SetValue("guid", uuid);
      key.SetValue("password", hashedPassword);
      key.SetValue("usageDate", DateOnly.FromDateTime(DateTime.Today));
      key.SetValue("dailyTimeLimit", dailyTimeLimit);
      key.SetValue("todayTimeLimit", todayTimeLimit);
      key.SetValue("usedTime", usedTime);
      key.SetValue("bedtime", bedtime);
      key.SetValue("waketime", waketime);

      return 0;
    }

    public void AcceptDelta(API.Delta delta)
    {
      // Update local state with server response
      dailyTimeLimit = delta.dailyTimeLimit ?? dailyTimeLimit;
      todayTimeLimit = delta.todayTimeLimit ?? todayTimeLimit;
      usedTime = delta.usedTime ?? usedTime;
      usageDate = delta.usageDate ?? usageDate;
      bedtime = delta.bedtime ?? bedtime;
      waketime = delta.waketime ?? waketime;
      graceGiven = delta.graceGiven ?? graceGiven;
      syncAuthor = delta.syncAuthor;
    }

    public bool NewPassword()
    {
      string? newPassword = Prompt.ShowDialog("Enter a new parent password.", "AutoLogout", true);
      if (newPassword == null)
      {
        return false;
      }
      hashedPassword = BC.BCrypt.HashPassword(newPassword);
      SaveToRegistry();
      return true;
    }
    public bool CheckPassword()
    {
      string? password = Prompt.ShowDialog("Enter the parent password to continue.", "AutoLogout Settings", true);
      if (password == null) return false;
      if (BC.BCrypt.Verify(password, hashedPassword)) return true;
      else
      {
        MessageBox.Show("The password was incorrect", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return false;
      }
    }

    // API methods
    public async Task Sync()
    {
      if (OnlineMode)
      {
        await api.Sync(this);
      }
    }
    public async Task Deauth()
    {
      OnlineMode = false;
      if (!await api.Deauth(this))
      {
        OnlineMode = true;
      }
      else
      {
        Changed?.Invoke();
      }
      SaveToRegistry();
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
          AutoSize = true,
          BackColor = SystemColors.Control,
          Padding = new Padding(8),
        };


        Label textLabel = new() { MaximumSize = new Size(576, 100), AutoSize = true, Text = text, Padding = new() { Bottom = 10 } };
        textBox = new() { Width = 300 };
        if (sensitive) textBox.PasswordChar = '*';

        mainPanel.Controls.Add(textLabel);
        mainPanel.Controls.Add(textBox);

        Button confirmation = new() { Text = "Ok", AutoSize = true, DialogResult = DialogResult.OK };
        Button cancel = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        confirmation.Click += (sender, e) => { Close(); };

        buttonPanel.Controls.Add(confirmation);
        buttonPanel.Controls.Add(cancel);

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