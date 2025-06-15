using System.Configuration;
using System.Diagnostics;
using Microsoft.Win32;
using BC = BCrypt.Net;

namespace AutoLogout
{
  public class API
  {
    // Constants
    private string Url
    {
      get => Debugger.IsAttached ? "http://localhost:8787/api/" : "https://timelimit.yiays.com/api/";
    }
    private readonly string SupportedAPIVersion = "1";
    private bool UpdateWarned = false;
    private readonly HttpClient httpClient = new()
    {
      Timeout = TimeSpan.FromSeconds(10),
      DefaultRequestHeaders =
      {
        { "User-Agent", "AutoLogoutClient/1.0" },
        { "Accept", "application/json" }
      }
    };

    // Response models
    private struct ApiResult<T>
    {
      public bool success { get; set; }
      public HttpResponseMessage response { get; set; }
      public T? result { get; set; }
    }
    private struct SyncResult
    {
      public bool accepted { get; set; }
      public string? error { get; set; }
      public Delta? delta { get; set; }
    }
    private struct DeauthResult
    {
      public bool success { get; set; }
      public string? error { get; set; }
    }
    public struct Delta
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

    private async Task<ApiResult<T>> ApiCall<T>(
      string endpoint, HttpMethod method, StringContent? content, Guid? authKey
    )
    {
      if (authKey is not null && authKey != Guid.Empty)
      {
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authKey.ToString());
      }
      else
      {
        httpClient.DefaultRequestHeaders.Remove("Authorization");
      }

      HttpResponseMessage response;
      try
      {
        response = await httpClient.SendAsync(
          new HttpRequestMessage(method, Url + endpoint)
          {
            Content = content
          }
        );
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Failed to call {endpoint}: {ex.Message}");
        throw;
      }

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
        }
      }

      if (!response.IsSuccessStatusCode)
      {
        Console.WriteLine($"API call '{endpoint}' failed: {response.StatusCode} - {response.ReasonPhrase}");
        return new ApiResult<T> { success = false, response = response, result = default };
      }

      if (response.IsSuccessStatusCode)
      {
        string responseBody = await response.Content.ReadAsStringAsync();
        T? result = System.Text.Json.JsonSerializer.Deserialize<T>(responseBody);
        if (result == null)
        {
          Console.WriteLine("Failed to deserialize api result.");
          return new ApiResult<T> { success = false, response = response, result = default };
        }
        return new ApiResult<T> { success = true, response = response, result = result };
      }
      return new ApiResult<T> { success = false, response = response, result = default };
    }

    public async Task Sync(State state)
    {
      // Convert state to JSON and share with online service
      string json = System.Text.Json.JsonSerializer.Serialize(new
      {
        state.hashedPassword,
        state.dailyTimeLimit,
        state.remainingTime,
        state.usedTime,
        state.remainingTimeDay,
        state.bedtime,
        state.waketime,
        state.graceGiven,
        state.syncAuthor
      });

      // Clear syncAuthor as we only need to acknowledge serverside changes once
      state.syncAuthor = null;

      var apiResponse = await ApiCall<SyncResult>(
        "sync/" + state.uuid.ToString(), HttpMethod.Post,
        new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        state.authKey
      );

      if (apiResponse.success)
      {
        if (apiResponse.result.accepted)
        {
          // Sync was successful, no changes needed
          if (apiResponse.result.delta != null)
          {
            // Server must have provided us with an authKey
            state.authKey = apiResponse.result.delta?.authKey ?? state.authKey;
            state.SaveToRegistry();
            Console.WriteLine("Recieved new authKey");
          }
        }
        else
        {
          // Sync was rejected
          if (apiResponse.result.error != null)
          {
            Console.WriteLine($"Sync failed: {apiResponse.result.error}");
          }
          else if (apiResponse.result.delta != null)
          {
            Console.WriteLine("Accepting alternative state from server");
            state.AcceptDelta(apiResponse.result.delta?? new Delta());
            state.TriggerStateChanged();
          }
        }
      }
    }

    public async Task<bool> Deauth(State state)
    {
      // Request the server deletes all client data
      var apiResult = await ApiCall<DeauthResult>(
        "deauth/" + state.uuid.ToString(), HttpMethod.Delete, null, state.authKey
      );
      if (apiResult.success)
      {
        state.authKey = Guid.Empty;
        state.SaveToRegistry();
        MessageBox.Show(
          "All devices which control this computer have been signed out.",
          "Success",
          MessageBoxButtons.OK,
          MessageBoxIcon.Information
        );
        return true;
      }
      else
      {
        MessageBox.Show(
          "Failed to sign all users out. Please try again later.",
          "Error",
          MessageBoxButtons.OK,
          MessageBoxIcon.Error
        );
        return false;
      }
    }
  }

  public class State
  {
    public bool OnlineMode = false;
    public bool Paused = false;
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
      RegistryKey? key = Registry.CurrentUser.CreateSubKey("Software\\Yiays\\AutoLogout", true);
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

      key.SetValue("OnlineMode", OnlineMode);
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

    public void AcceptDelta(API.Delta delta)
    {
      // Update local state with server response
      dailyTimeLimit = delta.dailyTimeLimit ?? dailyTimeLimit;
      remainingTime = delta.remainingTime ?? remainingTime;
      usedTime = delta.usedTime ?? usedTime;
      remainingTimeDay = delta.remainingTimeDay ?? remainingTimeDay;
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