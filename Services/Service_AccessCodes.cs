using System.Security.Cryptography;
using MudBlazor;
using ZeniControlSuite.Models;

namespace ZeniControlSuite.Services;

public sealed class Service_AccessCodes : IHostedService, IDisposable
{
    private readonly Service_Logs _logs;
    private CancellationTokenSource? _cts;
    private Task? _rotationTask;
    private readonly object _lock = new();

    public Service_AccessCodes(Service_Logs logs)
    {
        _logs = logs;
    }

    public event Action? OnCodesChanged;

    public string AdminPassword { get; private set; } = "000000";
    public int VisitorCode { get; private set; }
    public string VisitorCodeDisplay => VisitorCode.ToString("D3");
    public DateTimeOffset VisitorCodeExpiresAt { get; private set; }
    public int VisitorCodeRotationMinutes { get; private set; } = 3;
    public string VisitorCodeOscAddress { get; private set; } = "/avatar/parameters/ZCS_VisitorCode";

    public Parameter VisitorCodeParameter => new Parameter(VisitorCodeOscAddress, ParameterType.Int, VisitorCode);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        LoadConfig();
        AdminPassword = GenerateAdminPassword();
        RotateVisitorCode(notify: false);

        _logs.AddLog("Service_AccessCodes", "System", $"Generated admin password: {AdminPassword}", Severity.Warning, Variant.Outlined);
        _logs.AddLog("Service_AccessCodes", "System", $"Visitor code: {VisitorCodeDisplay}", Severity.Info, Variant.Outlined);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _rotationTask = Task.Run(() => RotationLoop(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts == null)
        {
            return;
        }

        await _cts.CancelAsync();
        if (_rotationTask != null)
        {
            try
            {
                await _rotationTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
        }
    }

    public bool VerifyAdminPassword(string? password)
    {
        return string.Equals(password?.Trim(), AdminPassword, StringComparison.Ordinal);
    }

    public bool VerifyVisitorCode(string? code)
    {
        var normalized = code?.Trim();
        return string.Equals(normalized, VisitorCodeDisplay, StringComparison.Ordinal);
    }

    public void RefreshAdminPassword()
    {
        AdminPassword = GenerateAdminPassword();
        _logs.AddLog("Service_AccessCodes", "System", $"Generated admin password refreshed: {AdminPassword}", Severity.Warning, Variant.Outlined);
        OnCodesChanged?.Invoke();
    }

    public void RefreshVisitorCode()
    {
        RotateVisitorCode(notify: true);
    }

    private async Task RotationLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var delay = VisitorCodeExpiresAt - DateTimeOffset.UtcNow;
            if (delay < TimeSpan.FromSeconds(5))
            {
                delay = TimeSpan.FromSeconds(5);
            }

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            RotateVisitorCode(notify: true);
        }
    }

    private void RotateVisitorCode(bool notify)
    {
        lock (_lock)
        {
            VisitorCode = RandomNumberGenerator.GetInt32(0, 255);
            VisitorCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, VisitorCodeRotationMinutes));
        }

        _logs.AddLog("Service_AccessCodes", "System", $"Visitor code rotated: {VisitorCodeDisplay}", Severity.Info, Variant.Outlined);
        if (notify)
        {
            OnCodesChanged?.Invoke();
        }
    }

    private static string GenerateAdminPassword()
    {
        return RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
    }

    private void LoadConfig()
    {
        try
        {
            if (!File.Exists("Configs/AccessCodes.json"))
            {
                CreateDefaultConfig();
            }

            var json = File.ReadAllText("Configs/AccessCodes.json");
            var config = System.Text.Json.JsonSerializer.Deserialize<AccessCodeConfig>(json) ?? new AccessCodeConfig();

            VisitorCodeRotationMinutes = Math.Clamp(config.VisitorCodeRotationMinutes ?? config.TemporaryCodeRotationMinutes ?? 3, 1, 60);
            VisitorCodeOscAddress = FirstNonEmpty(config.VisitorCodeOscAddress, config.TemporaryCodeOscAddress, VisitorCodeOscAddress);
        }
        catch (Exception ex)
        {
            _logs.AddLog("Service_AccessCodes", "System", $"AccessCodes.json loading failed: {ex.Message}", Severity.Error, Variant.Outlined);
        }
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static void CreateDefaultConfig()
    {
        Directory.CreateDirectory("Configs");
        var config = new AccessCodeConfig
        {
            VisitorCodeRotationMinutes = 3,
            VisitorCodeOscAddress = "/avatar/parameters/ZCS_VisitorCode"
        };
        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("Configs/AccessCodes.json", json);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private sealed class AccessCodeConfig
    {
        public int? VisitorCodeRotationMinutes { get; set; }
        public string? VisitorCodeOscAddress { get; set; }

        public int? TemporaryCodeRotationMinutes { get; set; }
        public string? TemporaryCodeOscAddress { get; set; }
    }
}
