using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using ZeniControlSuite.Authentication;
using ZeniControlSuite.Models;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components;

public partial class Panel_IntifaceMain : IDisposable
{
    public static bool pageEnabled = false;

    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = default!;
    [Inject] private Service_Logs LogService { get; set; } = default!;
    [Inject] private Service_Points PointsService { get; set; } = default!;
    [Inject] private Service_Intiface IntifaceService { get; set; } = default!;

    private string user = "Undefined";
    private string pageName = "Intiface";
    private bool isAdmin;
    private bool isLocalHost;
    private bool editMode;
    private bool showPatternControls;

    private static readonly PatternType[] AvailablePatterns =
    {
        PatternType.None,
        PatternType.Pulse,
        PatternType.Wave,
        PatternType.RampUp,
        PatternType.RampDown,
        PatternType.Saw,
        PatternType.Sine,
        PatternType.Tremor,
        PatternType.Burst,
        PatternType.RandomPulse
    };

    private const double DoubleStepMedium = 0.1;
    private const double DoubleMinPositive = 0.1;
    private const double DoubleMaxTen = 10.0;

    private const float FloatStepSmall = 0.05f;
    private const float FloatStepMedium = 0.1f;
    private const float FloatMinZero = 0f;
    private const float FloatMinPositive = 0.1f;
    private const float FloatMaxTen = 10f;

    private bool CanStart => isLocalHost && IntifaceService.IntifaceEnabled && !IntifaceService.IntifaceRunning;
    private bool HideForVisitor => !isAdmin && !IntifaceService.IntifaceRunning;
    private string ConnectionText
    {
        get
        {
            if (!IntifaceService.IntifaceEnabled) return "Disabled";
            if (IntifaceService.IntifaceConnected) return $"Connected · {IntifaceService.ConnectedDeviceCount} device(s)";
            if (IntifaceService.IntifaceRunning) return "Connecting";
            return "Stopped";
        }
    }

    protected override async Task OnInitializedAsync()
    {
        PointsService.OnPointsUpdate += OnPointsUpdate;
        IntifaceService.OnIntifaceControlsUpdate += OnIntifaceControlsUpdate;
        IntifaceService.OnIntifaceReadoutUpdate += OnIntifaceControlsUpdate;

        var authState = await AuthProvider.GetAuthenticationStateAsync();
        user = authState.GetUserName();
        isAdmin = authState.User.IsInRole("Admin");
        isLocalHost = authState.User.IsInRole("LocalHost") || SuiteClaims.IsAdminPasswordUser(authState.User);
        Log("PageLoad", Severity.Normal);
    }

    private void OnIntifaceControlsUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    private void OnPointsUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        PointsService.OnPointsUpdate -= OnPointsUpdate;
        IntifaceService.OnIntifaceControlsUpdate -= OnIntifaceControlsUpdate;
        IntifaceService.OnIntifaceReadoutUpdate -= OnIntifaceControlsUpdate;
    }

    private void Log(string message, Severity severity)
    {
        LogService.AddLog(pageName, user, message, severity);
    }

    private async Task StartIntiface()
    {
        await IntifaceService.IntifaceStart();
        Log("Intiface Starting", Severity.Normal);
    }

    private async Task StopIntiface()
    {
        await IntifaceService.IntifaceStop();
        Log("Intiface Stopping", Severity.Normal);
    }

    private async Task ToggleDeviceScanning()
    {
        if (!IntifaceService.DeviceScanning)
        {
            await IntifaceService.StartScanning();
            Log("Device Scanning Started", Severity.Normal);
        }
        else
        {
            await IntifaceService.StopScanning();
            Log("Device Scanning Stopped", Severity.Normal);
        }
    }

    private void ToggleEditMode()
    {
        editMode = !editMode;
    }

    private void TogglePatternControls()
    {
        showPatternControls = !showPatternControls;
    }

    private void PowerFullStop()
    {
        IntifaceService.FullStop = !IntifaceService.FullStop;
        Log("Full Stop: " + IntifaceService.FullStop, Severity.Normal);
    }

    private void SetPattern(PatternType patternType)
    {
        IntifaceService.PatternType = patternType;
        IntifaceService.PatternsEnabled = patternType != PatternType.None;
        if (patternType == PatternType.None)
        {
            IntifaceService.PatternPower = 1.0;
        }
        Log("Pattern: " + patternType, Severity.Normal);
    }

    private static string PatternLabel(PatternType patternType)
    {
        return patternType switch
        {
            PatternType.None => "Manual",
            PatternType.RampUp => "Ramp Up",
            PatternType.RampDown => "Ramp Down",
            PatternType.RandomPulse => "Random",
            _ => patternType.ToString()
        };
    }

    private static Color PatternColor(PatternType patternType)
    {
        return patternType switch
        {
            PatternType.None => Color.Default,
            PatternType.Pulse => Color.Primary,
            PatternType.Wave => Color.Secondary,
            PatternType.RampUp or PatternType.RampDown or PatternType.Saw => Color.Info,
            PatternType.Sine or PatternType.Tremor => Color.Tertiary,
            PatternType.Burst or PatternType.RandomPulse => Color.Warning,
            _ => Color.Default
        };
    }

    private void SaveIntifaceConfig()
    {
        IntifaceService.SaveConfig();
        Log("Device config saved", Severity.Normal);
    }

    private void AddDevice()
    {
        IntifaceService.AddConfiguredDevice();
    }

    private void RemoveDevice(IntifaceDevice device)
    {
        IntifaceService.RemoveConfiguredDevice(device);
    }

    private void AddHapticInput()
    {
        IntifaceService.AddHapticInput();
    }

    private void RemoveHapticInput(HapticInput input)
    {
        IntifaceService.RemoveHapticInput(input);
    }

    private static string ParameterName(HapticInput input)
    {
        return Service_Intiface.StripAvatarPrefix(input.Parameter.Address);
    }

    private void SetHapticParameterName(HapticInput input, string value)
    {
        input.Parameter.Address = Service_Intiface.NormalizeAvatarParameter(value);
        IntifaceService.RebuildHapticParameterIndex();
    }

    private void SetHapticType(HapticInput input, ParameterType value)
    {
        input.Parameter.Type = value;
        IntifaceService.RebuildHapticParameterIndex();
    }

    private static string GetHapticPreview(HapticInput input)
    {
        var value = input.Parameter.Type switch
        {
            ParameterType.Bool => input.Parameter.Value > 0.5f ? 1f : 0f,
            ParameterType.Int or ParameterType.Float => NormalizeHapticValue(input),
            _ => 0f
        };

        value = Math.Clamp(value * input.Multiplier * input.Influence, 0f, 1f);
        return value.ToString("0.###");
    }

    private static float NormalizeHapticValue(HapticInput input)
    {
        var range = input.Max - input.Min;
        if (Math.Abs(range) < 0.0001f)
        {
            return 0f;
        }

        var normalized = Math.Clamp((input.Parameter.Value - input.Min) / range, 0f, 1f);
        return (float)Math.Pow(normalized, input.Exponent);
    }
}
