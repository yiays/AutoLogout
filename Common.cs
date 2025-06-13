using System.Diagnostics;
using Microsoft.Win32;

namespace AutoLogout
{
  public class SyncResult
  {
    public bool accepted { get; set; }
    public string? error { get; set; }
    public SyncState? diff { get; set; }
  }

  public class SyncState
  {
    // State as it appears when returned from the server
    public Guid? authKey { get; set; }
    public Guid? uuid { get; set; }
    public string? hashedPassword { get; set; }
    public int? dailyTimeLimit { get; set; }
    public int? remainingTime { get; set; }
    public int? usedTime { get; set; }
    public DateOnly? remainingTimeDay { get; set; }
    public TimeOnly? bedtime { get; set; }
    public TimeOnly? waketime { get; set; }
    public bool? graceGiven { get; set; }
    public Guid? syncAuthor { get; set; }
    // syncAuthor tracks the last client that modified the state
    // This should be null whenever this client is modifying the state
  }

  public class State
  {
    // API related constants
    private static string SyncUrl
    {
      get => Debugger.IsAttached ? "http://localhost:8787/api/sync/" : "https://timelimit.yiays.com/api/sync/";
    }
    private static readonly string SupportedAPIVersion = "1";
    private static bool UpdateWarned = false;

    public Guid authKey = Guid.Empty;
    public Guid uuid = Guid.Empty;
    public string hashedPassword = "";
    public int dailyTimeLimit = -1;
    public int remainingTime = -1;
    public int usedTime = 0;
    public DateOnly remainingTimeDay = DateOnly.FromDateTime(DateTime.Today);
    public TimeOnly bedtime = new TimeOnly(0, 0);
    public TimeOnly waketime = new TimeOnly(0, 0);
    public bool graceGiven = false;
    public Guid? syncAuthor = null;

    public event Action? StateChanged;

    private static readonly HttpClient httpClient = new()
    {
      Timeout = TimeSpan.FromSeconds(10)
    };

    public void NewGuid()
    {
      uuid = Guid.NewGuid();
    }

    public int FromRegistry()
    {
      RegistryKey? key = Registry.CurrentUser.CreateSubKey("Software\\Yiays\\AutoLogout", true);
      if (key == null)
      {
        MessageBox.Show("Unable to access settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        remainingTime = 0;
        return -1;
      }

      // Load current app state from registry
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
      remainingTimeDay = DateOnly.Parse((string)key.GetValue("remainingTimeDay", "1/01/0001"));
      remainingTime = (int)key.GetValue("remainingTime", -1);
      usedTime = (int)key.GetValue("usedTime", 0);

      return 0;
    }

    public int SaveToRegistry()
    {
      RegistryKey? key = Registry.CurrentUser.CreateSubKey("Software\\Yiays\\AutoLogout");
      if (key == null)
      {
        MessageBox.Show("Unable to save settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        remainingTime = 0;
        return -1;
      }

      key.SetValue("authKey", authKey);
      key.SetValue("guid", uuid);
      key.SetValue("password", hashedPassword);
      key.SetValue("remainingTimeDay", DateOnly.FromDateTime(DateTime.Today));
      key.SetValue("dailyTimeLimit", dailyTimeLimit);
      key.SetValue("remainingTime", remainingTime);
      key.SetValue("usedTime", usedTime);
      key.SetValue("bedtime", bedtime);
      key.SetValue("waketime", waketime);

      return 0;
    }

    private void AcceptDiff(SyncState diff)
    {
      // Update local state with server response
      dailyTimeLimit = diff.dailyTimeLimit ?? dailyTimeLimit;
      remainingTime = diff.remainingTime ?? remainingTime;
      usedTime = diff.usedTime ?? usedTime;
      remainingTimeDay = diff.remainingTimeDay ?? remainingTimeDay;
      bedtime = diff.bedtime ?? bedtime;
      waketime = diff.waketime ?? waketime;
      graceGiven = diff.graceGiven ?? graceGiven;
      syncAuthor = diff.syncAuthor;
    }

    public async Task Sync()
    {
      // Convert state to JSON and share with online service
      try
      {
        string json = System.Text.Json.JsonSerializer.Serialize(new
        {
          authKey,
          hashedPassword,
          dailyTimeLimit,
          remainingTime,
          usedTime,
          remainingTimeDay,
          bedtime,
          waketime,
          graceGiven,
          syncAuthor
        });

        // Clear syncAuthor as we only need to acknowledge serverside changes once
        syncAuthor = null;

        Console.WriteLine("Syncing state: " + json);

        HttpResponseMessage response = await httpClient.PostAsync(
          SyncUrl + uuid,
          new StringContent(json, System.Text.Encoding.UTF8,
          "application/json")
        );

        if (response.Headers.TryGetValues("X-Api-Version", out var apiVersionHeaders))
        {
          string apiVersion = apiVersionHeaders.FirstOrDefault() ?? "";
          if (apiVersion != SupportedAPIVersion)
          {
            if (!UpdateWarned)
            {
              Console.WriteLine($"It appears this client is out of date. Expected API version: {SupportedAPIVersion}, got: {apiVersion}");
              UpdateWarned = true;
              _ = Task.Run(() =>
              {
                MessageBox.Show(
                  "Please update to the latest version to ensure online features work.",
                  "AutoLogout is out of date",
                  MessageBoxButtons.OK,
                  MessageBoxIcon.Warning
                );
              });
            }
            return;
          }
        }

        if (response.IsSuccessStatusCode)
        {
          string responseBody = await response.Content.ReadAsStringAsync();
          SyncResult? result = System.Text.Json.JsonSerializer.Deserialize<SyncResult>(responseBody);
          if (result == null)
          {
            Console.WriteLine("Failed to deserialize sync result.");
          }
          else if (result.accepted)
          {
            // Sync was successful, no changes needed
            if (result.diff != null)
            {
              // Server must have provided us with an authKey
              authKey = result.diff.authKey ?? authKey;
              SaveToRegistry();
              Console.WriteLine("Recieved new authKey");
            }
          }
          else
          {
            // Sync was rejected
            if (result.error != null)
            {
              Console.WriteLine($"Sync failed: {result.error}");
            }
            else if (result.diff != null)
            {
              AcceptDiff(result.diff);
              StateChanged?.Invoke();
            }
          }
        }
        else
        {
          Console.WriteLine($"Sync failed: {response.StatusCode} - {response.ReasonPhrase}");
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Failed to sync state: {ex.Message}");
      }
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