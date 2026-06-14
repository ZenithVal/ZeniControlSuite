namespace ZeniControlSuite.Models;

public enum OpenShockControlType
{
    Shock,
    Vibrate
}

public sealed class OpenShockConfig
{
    public bool Enabled { get; set; }
    public string ApiBaseUrl { get; set; } = "https://api.openshock.app";
    public string ApiToken { get; set; } = string.Empty;
    public string UserAgent { get; set; } = "ZeniControlSuite/1.0";
    public List<OpenShockDeviceConfig> Shockers { get; set; } = new();
    public OpenShockManualConfig Manual { get; set; } = new();
    public OpenShockOscStatusConfig OscStatus { get; set; } = new();
    public List<OpenShockOscTriggerConfig> OscTriggers { get; set; } = new();
}

public sealed class OpenShockDeviceConfig
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "Shocker";
    public string Id { get; set; } = string.Empty;
}

public sealed class OpenShockManualConfig
{
    public float Intensity { get; set; } = 25f;
    public float DurationSeconds { get; set; } = 1f;
    public float MinIntensity { get; set; } = 1f;
    public float MaxIntensity { get; set; } = 100f;
    public float MinDurationSeconds { get; set; } = 0.5f;
    public float MaxDurationSeconds { get; set; } = 10f;
    public bool AutomatedWarningEnabled { get; set; }
    public float WarningIntensity { get; set; } = 25f;
    public float WarningDurationSeconds { get; set; } = 0.5f;
    public float WarningDelayMinSeconds { get; set; } = 0.5f;
    public float WarningDelayMaxSeconds { get; set; } = 2f;
}

public sealed class OpenShockOscStatusConfig
{
    public string ActiveAddress { get; set; } = "ShockOSC/Any_Active";
    public string CooldownAddress { get; set; } = "ShockOsc/Any_Cooldown";
}

public sealed class OpenShockOscTriggerConfig
{
    public bool Enabled { get; set; } = true;
    public string Address { get; set; } = "ShockOsc/_All_IShock";
    public float MinIntensity { get; set; } = 50f;
    public float MaxIntensity { get; set; } = 80f;
    public float MinDurationSeconds { get; set; } = 0.5f;
    public float MaxDurationSeconds { get; set; } = 1.5f;
    public float CooldownSeconds { get; set; } = 5f;
    public DateTimeOffset LastTriggered { get; set; } = DateTimeOffset.MinValue;
}

public sealed class OpenShockEventLog
{
    public DateTimeOffset Time { get; set; } = DateTimeOffset.Now;
    public string Message { get; set; } = string.Empty;
    public MudBlazor.Severity Severity { get; set; } = MudBlazor.Severity.Normal;
}
