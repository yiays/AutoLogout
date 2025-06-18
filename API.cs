using System.Diagnostics;

namespace AutoLogout
{
  public class API
  {
    // Constants
    private string Url
    {
      get => Debugger.IsAttached ? "http://localhost:8787/api/" : "https://timelimit.yiays.com/api/";
    }
    private readonly string SupportedAPIVersion = "2";
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
      public int? todayTime { get; set; }
      public int? usedTime { get; set; }
      public DateOnly? usageDate { get; set; }
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
        state.usageDate,
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
            state.AcceptDelta(apiResponse.result.delta ?? new Delta());
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
}