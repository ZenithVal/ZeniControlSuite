using System.Text;
using System.Text.Json;
using MudBlazor;
using ZeniControlSuite.Models;

namespace ZeniControlSuite.Services;

public sealed class Service_OpenShock : IHostedService, IDisposable
{
    private readonly Service_Logs LogService;
    private readonly Service_OSC OscService;
    private readonly HttpClient _httpClient = new();
    private readonly object _configLock = new();
    private readonly object _eventLock = new();
    private readonly Dictionary<string, bool> _lastOscBoolStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public Service_OpenShock(Service_Logs logService, Service_OSC oscService)
    {
        LogService = logService;
        OscService = oscService;
        OscService.OnOscMessageReceived += HandleOscMessage;
    }

    public event Action? OnOpenShockUpdate;

    public OpenShockConfig Config { get; private set; } = new();
    public IReadOnlyList<OpenShockEventLog> Events
    {
        get
        {
            lock (_eventLock)
            {
                return _events.OrderByDescending(log => log.Time).ToList();
            }
        }
    }

    private readonly List<OpenShockEventLog> _events = new();

    public bool HubOnline { get; private set; }
    public bool CommandRunning { get; private set; }
    public bool CooldownActive { get; private set; }
    public string LastStatus { get; private set; } = "Not checked";
    public bool CanControl => Config.Enabled
        && !string.IsNullOrWhiteSpace(Config.ApiToken)
        && Config.Shockers.Any(shocker => shocker.Enabled && !string.IsNullOrWhiteSpace(shocker.Id));

    public Task StartAsync(CancellationToken cancellationToken)
    {
        LoadConfig();
        RegisterOscEndpoints();
        _ = CheckHubStatusAsync();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        OscService.OnOscMessageReceived -= HandleOscMessage;
        _httpClient.Dispose();
    }

    public void LoadConfig()
    {
        lock (_configLock)
        {
            Directory.CreateDirectory("Configs");

            if (!File.Exists("Configs/OpenShock.json"))
            {
                Config = CreateDefaultConfig();
                SaveConfigUnlocked();
                AddEvent("Created default OpenShock config.", Severity.Info);
                return;
            }

            try
            {
                Config = JsonSerializer.Deserialize<OpenShockConfig>(File.ReadAllText("Configs/OpenShock.json")) ?? CreateDefaultConfig();
                NormalizeConfigUnlocked();
                AddEvent("OpenShock config loaded.", Severity.Normal);
            }
            catch (Exception ex)
            {
                Config = CreateDefaultConfig();
                HubOnline = false;
                LastStatus = "Config error";
                AddEvent($"OpenShock config load failed: {ex.Message}", Severity.Error);
            }
        }

        RegisterOscEndpoints();
        NotifyChanged();
    }

    public void SaveConfig()
    {
        lock (_configLock)
        {
            NormalizeConfigUnlocked();
            SaveConfigUnlocked();
        }

        RegisterOscEndpoints();
        AddEvent("OpenShock config saved.", Severity.Normal);
        NotifyChanged();
    }

    private void SaveConfigUnlocked()
    {
        Directory.CreateDirectory("Configs");
        File.WriteAllText("Configs/OpenShock.json", JsonSerializer.Serialize(Config, _jsonOptions));
    }

    private static OpenShockConfig CreateDefaultConfig()
    {
        return new OpenShockConfig
        {
            Enabled = false,
            Shockers = new List<OpenShockDeviceConfig>
            {
                new() { Enabled = false, Name = "Zeni A", Id = string.Empty }
            },
            OscTriggers = new List<OpenShockOscTriggerConfig>
            {
                new()
                {
                    Enabled = true,
                    Address = "ShockOsc/_All_IShock",
                    MinIntensity = 50f,
                    MaxIntensity = 80f,
                    MinDurationSeconds = 0.5f,
                    MaxDurationSeconds = 1.5f,
                    CooldownSeconds = 5f
                }
            }
        };
    }

    private void NormalizeConfigUnlocked()
    {
        Config.ApiBaseUrl = string.IsNullOrWhiteSpace(Config.ApiBaseUrl) ? "https://api.openshock.app" : Config.ApiBaseUrl.Trim().TrimEnd('/');
        Config.UserAgent = string.IsNullOrWhiteSpace(Config.UserAgent) ? "ZeniControlSuite/1.0" : Config.UserAgent.Trim();
        Config.Shockers ??= new List<OpenShockDeviceConfig>();
        Config.Manual ??= new OpenShockManualConfig();
        Config.OscStatus ??= new OpenShockOscStatusConfig();
        Config.OscTriggers ??= new List<OpenShockOscTriggerConfig>();

        Config.Manual.MinIntensity = Clamp(Config.Manual.MinIntensity, 1f, 100f);
        Config.Manual.MaxIntensity = Clamp(Config.Manual.MaxIntensity, Config.Manual.MinIntensity, 100f);
        Config.Manual.Intensity = Clamp(Config.Manual.Intensity, Config.Manual.MinIntensity, Config.Manual.MaxIntensity);
        Config.Manual.MinDurationSeconds = Math.Max(0.1f, Config.Manual.MinDurationSeconds);
        Config.Manual.MaxDurationSeconds = Math.Max(Config.Manual.MinDurationSeconds, Config.Manual.MaxDurationSeconds);
        Config.Manual.DurationSeconds = Clamp(Config.Manual.DurationSeconds, Config.Manual.MinDurationSeconds, Config.Manual.MaxDurationSeconds);
        Config.Manual.WarningIntensity = Clamp(Config.Manual.WarningIntensity, 1f, 100f);
        Config.Manual.WarningDurationSeconds = Math.Max(0.1f, Config.Manual.WarningDurationSeconds);
        Config.Manual.WarningDelayMinSeconds = Math.Max(0f, Config.Manual.WarningDelayMinSeconds);
        Config.Manual.WarningDelayMaxSeconds = Math.Max(Config.Manual.WarningDelayMinSeconds, Config.Manual.WarningDelayMaxSeconds);

        foreach (var trigger in Config.OscTriggers)
        {
            trigger.Address = StripAvatarPrefix(trigger.Address).Trim('/');
            trigger.MinIntensity = Clamp(trigger.MinIntensity, 1f, 100f);
            trigger.MaxIntensity = Clamp(trigger.MaxIntensity, trigger.MinIntensity, 100f);
            trigger.MinDurationSeconds = Math.Max(0.1f, trigger.MinDurationSeconds);
            trigger.MaxDurationSeconds = Math.Max(trigger.MinDurationSeconds, trigger.MaxDurationSeconds);
            trigger.CooldownSeconds = Math.Max(0f, trigger.CooldownSeconds);
        }

        Config.OscStatus.ActiveAddress = StripAvatarPrefix(Config.OscStatus.ActiveAddress).Trim('/');
        Config.OscStatus.CooldownAddress = StripAvatarPrefix(Config.OscStatus.CooldownAddress).Trim('/');
    }

    private static float Clamp(float value, float min, float max)
    {
        if (max < min) max = min;
        return Math.Min(max, Math.Max(min, value));
    }

    private void RegisterOscEndpoints()
    {
        try
        {
            foreach (var trigger in Config.OscTriggers.Where(trigger => !string.IsNullOrWhiteSpace(trigger.Address)))
            {
                OscService.RegisterOscQueryEndpoint(ToAvatarParameterAddress(trigger.Address), "b", "OpenShock trigger");
            }

            if (!string.IsNullOrWhiteSpace(Config.OscStatus.ActiveAddress))
            {
                OscService.RegisterOscQueryEndpoint(ToAvatarParameterAddress(Config.OscStatus.ActiveAddress), "b", "OpenShock active state");
            }

            if (!string.IsNullOrWhiteSpace(Config.OscStatus.CooldownAddress))
            {
                OscService.RegisterOscQueryEndpoint(ToAvatarParameterAddress(Config.OscStatus.CooldownAddress), "b", "OpenShock cooldown state");
            }
        }
        catch (Exception ex)
        {
            AddEvent($"OSC endpoint registration failed: {ex.Message}", Severity.Warning);
        }
    }

    public void SetManualIntensity(double value)
    {
        Config.Manual.Intensity = Clamp((float)value, Config.Manual.MinIntensity, Config.Manual.MaxIntensity);
        NotifyChanged();
    }

    public void SetManualDuration(double value)
    {
        Config.Manual.DurationSeconds = Clamp((float)value, Config.Manual.MinDurationSeconds, Config.Manual.MaxDurationSeconds);
        NotifyChanged();
    }

    public async Task ManualWarningAsync()
    {
        await ExecuteCommandAsync(OpenShockControlType.Vibrate, Config.Manual.WarningIntensity, Config.Manual.WarningDurationSeconds, "Manual warning", false);
    }

    public async Task ManualShockAsync()
    {
        if (Config.Manual.AutomatedWarningEnabled)
        {
            await ExecuteCommandAsync(OpenShockControlType.Vibrate, Config.Manual.WarningIntensity, Config.Manual.WarningDurationSeconds, "Automated warning", false);
            var delay = Random.Shared.NextDouble() * Math.Max(0, Config.Manual.WarningDelayMaxSeconds - Config.Manual.WarningDelayMinSeconds) + Config.Manual.WarningDelayMinSeconds;
            AddEvent($"Automated warning delay: {delay:0.00}s", Severity.Info);
            await Task.Delay(TimeSpan.FromSeconds(delay));
        }

        await ExecuteCommandAsync(OpenShockControlType.Shock, Config.Manual.Intensity, Config.Manual.DurationSeconds, "Manual shock", true);
    }

    public async Task CheckHubStatusAsync()
    {
        if (string.IsNullOrWhiteSpace(Config.ApiToken))
        {
            HubOnline = false;
            LastStatus = "Missing token";
            NotifyChanged();
            return;
        }

        var candidates = new[]
        {
            "2/shockers",
            "2/users/self",
            "2/users/me",
            "1/shockers",
            "1/users/self",
            "1/users/me"
        };

        foreach (var candidate in candidates)
        {
            try
            {
                using var request = CreateRequest(HttpMethod.Get, candidate);
                using var response = await _httpClient.SendAsync(request);
                var statusCode = (int)response.StatusCode;

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    continue;
                }

                HubOnline = response.IsSuccessStatusCode;
                LastStatus = response.IsSuccessStatusCode
                    ? "API connected"
                    : $"API status {statusCode}";

                AddEvent($"{LastStatus} ({candidate})", response.IsSuccessStatusCode ? Severity.Success : Severity.Warning);
                NotifyChanged();
                return;
            }
            catch (Exception ex)
            {
                HubOnline = false;
                LastStatus = ex.Message;
                AddEvent($"OpenShock status check failed: {ex.Message}", Severity.Warning);
                NotifyChanged();
                return;
            }
        }

        HubOnline = false;
        LastStatus = "API status endpoint not found";
        AddEvent("OpenShock status check failed: no known status endpoint was available.", Severity.Warning);
        NotifyChanged();
    }

    private void HandleOscMessage(OscMessage message)
    {
        if (!Config.Enabled || Config.OscTriggers.Count == 0)
        {
            return;
        }

        var normalizedAddress = StripAvatarPrefix(message.Address);
        var value = ToBool(message.FirstOrDefault());
        if (!_lastOscBoolStates.TryGetValue(normalizedAddress, out var previous))
        {
            previous = false;
        }

        _lastOscBoolStates[normalizedAddress] = value;

        if (!value || previous)
        {
            return;
        }

        var trigger = Config.OscTriggers.FirstOrDefault(candidate =>
            candidate.Enabled && string.Equals(StripAvatarPrefix(candidate.Address), normalizedAddress, StringComparison.OrdinalIgnoreCase));

        if (trigger == null)
        {
            return;
        }

        _ = Task.Run(() => ExecuteOscTriggerAsync(trigger));
    }

    private async Task ExecuteOscTriggerAsync(OpenShockOscTriggerConfig trigger)
    {
        var now = DateTimeOffset.UtcNow;
        var cooldownRemaining = trigger.LastTriggered == DateTimeOffset.MinValue
            ? TimeSpan.Zero
            : trigger.LastTriggered.AddSeconds(trigger.CooldownSeconds) - now;

        if (cooldownRemaining > TimeSpan.Zero)
        {
            AddEvent($"OSC trigger {trigger.Address} ignored: cooldown {cooldownRemaining.TotalSeconds:0.0}s.", Severity.Warning);
            SendCooldownState(true);
            return;
        }

        trigger.LastTriggered = now;
        var intensity = RandomFloat(trigger.MinIntensity, trigger.MaxIntensity);
        var duration = RandomFloat(trigger.MinDurationSeconds, trigger.MaxDurationSeconds);

        SendCooldownState(true);
        await ExecuteCommandAsync(OpenShockControlType.Shock, intensity, duration, $"OSC {trigger.Address}", true);
        _ = ClearCooldownAfterDelayAsync(trigger.CooldownSeconds);
    }

    private async Task ClearCooldownAfterDelayAsync(float seconds)
    {
        if (seconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
        }

        SendCooldownState(false);
    }

    private async Task ExecuteCommandAsync(OpenShockControlType type, float intensity, float durationSeconds, string source, bool updateActiveState)
    {
        if (!CanControl)
        {
            HubOnline = false;
            LastStatus = "OpenShock is not configured.";
            AddEvent($"{source} skipped: OpenShock is not configured.", Severity.Warning);
            NotifyChanged();
            return;
        }

        intensity = Clamp(intensity, 1f, 100f);
        durationSeconds = Math.Max(0.1f, durationSeconds);

        if (updateActiveState)
        {
            CommandRunning = true;
            SendActiveState(true);
            NotifyChanged();
        }

        try
        {
            var shocks = Config.Shockers
                .Where(shocker => shocker.Enabled && !string.IsNullOrWhiteSpace(shocker.Id))
                .Select(shocker => new
                {
                    id = shocker.Id,
                    type = type.ToString(),
                    intensity = (int)Math.Round(intensity),
                    duration = (int)Math.Round(durationSeconds * 1000f),
                    exclusive = true
                })
                .ToArray();

            var payload = new
            {
                shocks
            };

            using var response = await SendControlRequestAsync(payload);
            var responseText = await response.Content.ReadAsStringAsync();

            HubOnline = response.IsSuccessStatusCode;
            LastStatus = response.IsSuccessStatusCode ? "Command sent" : $"Command failed {(int)response.StatusCode}";

            if (response.IsSuccessStatusCode)
            {
                AddEvent($"{source}: {type} {intensity:0}% for {durationSeconds:0.00}s.", type == OpenShockControlType.Shock ? Severity.Warning : Severity.Info);
            }
            else
            {
                AddEvent($"{source} failed: {(int)response.StatusCode} {TrimForLog(responseText)}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            HubOnline = false;
            LastStatus = ex.Message;
            AddEvent($"{source} failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            if (updateActiveState)
            {
                await Task.Delay(TimeSpan.FromSeconds(durationSeconds));
                CommandRunning = false;
                SendActiveState(false);
                NotifyChanged();
            }
        }
    }

    private async Task<HttpResponseMessage> SendControlRequestAsync(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var primary = CreateRequest(HttpMethod.Post, "2/shockers/control");
        primary.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(primary);
        if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            return response;
        }

        response.Dispose();

        var fallback = CreateRequest(HttpMethod.Post, "1/shockers/control");
        fallback.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _httpClient.SendAsync(fallback);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        var baseUrl = Config.ApiBaseUrl.TrimEnd('/');
        var uri = new Uri($"{baseUrl}/{relativePath.TrimStart('/')}");
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("Open-Shock-Token", Config.ApiToken);
        request.Headers.TryAddWithoutValidation("User-Agent", Config.UserAgent);
        return request;
    }

    private void SendActiveState(bool value)
    {
        if (string.IsNullOrWhiteSpace(Config.OscStatus.ActiveAddress))
        {
            return;
        }

        OscService.sendOSCMessage(ToAvatarParameterAddress(Config.OscStatus.ActiveAddress), value);
    }

    private void SendCooldownState(bool value)
    {
        CooldownActive = value;
        if (!string.IsNullOrWhiteSpace(Config.OscStatus.CooldownAddress))
        {
            OscService.sendOSCMessage(ToAvatarParameterAddress(Config.OscStatus.CooldownAddress), value);
        }

        NotifyChanged();
    }

    private void AddEvent(string message, Severity severity)
    {
        lock (_eventLock)
        {
            _events.Add(new OpenShockEventLog { Message = message, Severity = severity });
            if (_events.Count > 100)
            {
                _events.RemoveAt(0);
            }
        }

        LogService.AddLog("Service_OpenShock", "System", message, severity, Variant.Outlined);
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        OnOpenShockUpdate?.Invoke();
    }

    public void AddShocker()
    {
        Config.Shockers.Add(new OpenShockDeviceConfig { Enabled = false, Name = "New Shocker", Id = string.Empty });
        SaveConfig();
    }

    public void RemoveShocker(OpenShockDeviceConfig shocker)
    {
        Config.Shockers.Remove(shocker);
        SaveConfig();
    }

    public void AddOscTrigger()
    {
        Config.OscTriggers.Add(new OpenShockOscTriggerConfig());
        SaveConfig();
    }

    public void RemoveOscTrigger(OpenShockOscTriggerConfig trigger)
    {
        Config.OscTriggers.Remove(trigger);
        SaveConfig();
    }

    private static string ToAvatarParameterAddress(string address)
    {
        var stripped = StripAvatarPrefix(address).Trim('/');
        return $"/avatar/parameters/{stripped}";
    }

    private static string StripAvatarPrefix(string address)
    {
        const string prefix = "/avatar/parameters/";
        if (address.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return address[prefix.Length..];
        }

        return address.TrimStart('/');
    }

    private static bool ToBool(object? value)
    {
        return value switch
        {
            bool boolValue => boolValue,
            int intValue => intValue != 0,
            long longValue => longValue != 0,
            float floatValue => Math.Abs(floatValue) > 0.0001f,
            double doubleValue => Math.Abs(doubleValue) > 0.0001,
            string stringValue => bool.TryParse(stringValue, out var parsed) && parsed,
            _ => false
        };
    }

    private static float RandomFloat(float min, float max)
    {
        if (max < min)
        {
            (min, max) = (max, min);
        }

        return (float)(Random.Shared.NextDouble() * (max - min) + min);
    }

    private static string TrimForLog(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = text.ReplaceLineEndings(" ").Trim();
        return text.Length > 180 ? text[..180] + "..." : text;
    }
}
