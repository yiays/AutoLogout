using Avalonia.Threading;
using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AutoLogout
{
    public class AppAPI
    {
        // Constants
#if DEBUG
        private string Url = "http://localhost:8111/api/";
#else
        private string Url = "https://autologout.yiays.com/api/";
#endif
        private static readonly string API_VERSION = "3";
        private static readonly string UASTRING =
            $"AutoLogoutClient/{API_VERSION} (AutoLogout {State.Current.Version}) ({RuntimeInformation.OSDescription})";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new DateOnlyWithOffsetJsonConverter() }
        };
        private readonly HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10),
            DefaultRequestHeaders =
            {
                { "User-Agent", UASTRING },
                { "Accept", "application/json" }
            }
        };
        public DispatcherTimer syncTimer = new()
        {
            Interval = TimeSpan.FromSeconds(10)
        };

        public AppAPI()
        {
            syncTimer.Tick += async (o, e) =>
            {
                if(State.Current.Store.Online)
                    await Sync();
                else {
                    Console.WriteLine("Stopped sync timer as Online mode was off");
                    syncTimer.Stop();
                }
            };
        }

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
            public DeltaState? delta { get; set; }
        }

        private sealed class DateOnlyWithOffsetJsonConverter : JsonConverter<DateOnly>
        {
            public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException("Expected a string value for DateOnly.");
                }

                string? value = reader.GetString();
                if (string.IsNullOrWhiteSpace(value))
                {
                    return default;
                }

                string datePart = value.Trim();
                int separatorIndex = datePart.IndexOfAny(new[] { ' ', 'T' });
                if (separatorIndex >= 0)
                {
                    datePart = datePart[..separatorIndex];
                }

                return DateOnly.ParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
        }
        private struct DeauthResult
        {
            public bool success { get; set; }
            public string? error { get; set; }
        }
        public struct UpdateCheckResult
        {
            public string version { get; set; }
        }

        private async Task<ApiResult<T>> ApiCall<T>(
          string endpoint, HttpMethod method, StringContent? content, Guid? authKey
        )
        {
            if (authKey is not null && authKey != Guid.Empty)
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authKey.ToString());
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
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Failed to call {endpoint}: {ex.Message}");
                return new ApiResult<T> { success = false };
            }

            if (response.Headers.TryGetValues("X-Api-Version", out var apiVersionHeaders))
            {
                string apiVersion = apiVersionHeaders.FirstOrDefault() ?? "";
                if (apiVersion != API_VERSION)
                {
                    if (State.Current.Update != UpdateUrgency.Critical)
                    {
                        Console.WriteLine(
                            $"It appears this client is out of date. Expected API version: {API_VERSION}, got: {apiVersion}"
                        );
                        State.Current.Update = UpdateUrgency.Critical;
                    }
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"API call '{endpoint}' failed: {response.ReasonPhrase}");
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Console.Write(responseBody);
                }
                return new ApiResult<T> { success = false, response = response };
            }

            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                try {
                    T? result = JsonSerializer.Deserialize<T>(responseBody, JsonOptions);
                    if (result == null)
                    {
                        Console.WriteLine("Failed to deserialize API result.");
                        return new ApiResult<T> { success = false, response = response };
                    }
                    return new ApiResult<T> { success = true, response = response, result = result };
                }
                catch(JsonException e)
                {
                    Console.WriteLine("Error encountered deserializing API result: "+e.Message);
                }
            }
            return new ApiResult<T> { success = false, response = response };
        }

        public async Task<bool> Sync(Guid? syncAuthor = null)
        {
            // Convert state to JSON and share with online service
            TimeSpan offset = TimeZoneInfo.Local.BaseUtcOffset;
            string sign = offset < TimeSpan.Zero ? "-" : "+";
            string timezone = sign + (offset < TimeSpan.Zero ? -offset : offset).ToString(@"hh\:mm");
            string usageDate = State.Current.Store.usageDate.ToString(@"yyyy\-MM\-dd", CultureInfo.InvariantCulture) + ' ' + timezone;

            string json = JsonSerializer.Serialize(new
            {
                State.Current.Store.hashedPassword,
                State.Current.Store.dailyTimeLimit,
                State.Current.Store.todayTimeLimit,
                State.Current.Store.usedTime,
                usageDate,
                State.Current.Store.usage,
                State.Current.Store.bedtime,
                State.Current.Store.waketime,
                syncAuthor = syncAuthor is null ? State.Current.Store.authKey : syncAuthor
            });

            var apiResponse = await ApiCall<SyncResult>(
              "sync/" + State.Current.Store.uuid.ToString(), HttpMethod.Post,
              new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
              State.Current.Store.authKey
            );

            if (apiResponse.success)
            {
                if (apiResponse.result.accepted)
                {
                    // Sync was successful, no changes needed
                    if (apiResponse.result.delta is DeltaState delta)
                    {
                        // Server must have provided us with an authKey
                        if (delta.authKey is Guid authkey)
                        {
                            State.Current.Store.authKey = authkey;
                            await OS.Current.SaveState();
                            Console.WriteLine("Recieved new authKey");
                        }
                        else
                        {
                            string responseBody = await apiResponse.response.Content.ReadAsStringAsync();
                            throw new Exception("Unhandled delta\n" + responseBody);
                        }
                    }
                    return true;
                }
                else
                {
                    // Sync was rejected
                    if (apiResponse.result.error != null)
                    {
                        Console.WriteLine($"Sync failed: {apiResponse.result.error}");
                        return false;
                    }
                    else if (apiResponse.result.delta != null)
                    {
                        Console.WriteLine("Accepting alternative State.Current.Store from server");
                        State.Current.AcceptDelta(apiResponse.result.delta);
                        await OS.Current.SaveState();
                        // Sync again with the foreign syncAuthor id to acknowledge the new state
                        return await Sync(apiResponse.result.delta.syncAuthor);
                    }
                }
            }
#if DEBUG
            else
            {
                Console.WriteLine(json);
            }
#endif
            return false;
        }

        public async Task<bool> Deauth()
        {
            // Request the server deletes all client data
            var apiResult = await ApiCall<DeauthResult>(
              "deauth/" + State.Current.Store.uuid.ToString(), HttpMethod.Delete, null, State.Current.Store.authKey
            );
            return apiResult.success;
        }
        
        public async Task<UpdateCheckResult> UpdateCheck()
        {
            // Request the server deletes all client data
            var apiResult = await ApiCall<UpdateCheckResult>("update", HttpMethod.Get, null, null);
            if(apiResult.success)
                return apiResult.result;
            else return new UpdateCheckResult { version = "0.0.0" };
        }
    }

    public static class API
    {
        public static readonly AppAPI Current = new();
    }
}