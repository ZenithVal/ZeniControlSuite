﻿using Microsoft.AspNetCore.Components;
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
    private AuthenticationState context;
    private string pageName = "Intiface";

    protected override async Task OnInitializedAsync()
    {
        PointsService.OnPointsUpdate += OnPointsUpdate;
        IntifaceService.OnIntifaceControlsUpdate += OnIntifaceControlsUpdate;

        var context = await AuthProvider.GetAuthenticationStateAsync();
        user = context.GetUserName();
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
    }

    private void Log(string message, Severity severity)
    {
        LogService.AddLog(pageName, user, message, severity);
    }

    public void StartIntiface()
    {
        IntifaceService.IntifaceStart();
        Log("Intiface Starting", Severity.Normal);
    }

    public void StopIntiface()
    {
        IntifaceService.IntifaceStop();
        Log("Intiface Stopping", Severity.Normal);
    }

    public void ToggleDeviceScanning()
    {
        if (!IntifaceService.DeviceScanning)
        {
            IntifaceService.StartScanning();
            Log("Device Scanning Started", Severity.Normal);
        }
        else
        {
            IntifaceService.StopScanning();
            Log("Device Scanning Stopped", Severity.Normal);
        }
    }

    bool adminPanelExpand = false;
    private void ToggleAdminPanel()
    {
        adminPanelExpand = !adminPanelExpand;
    }

    bool patternPanelExpand = true;
    private void TogglePatternPanel()
    {
		patternPanelExpand = !patternPanelExpand;
	}

    bool hapticPanelExpand = true;
    private void ToggleHapticPanel()
    {
		hapticPanelExpand = !hapticPanelExpand;

	}

    public void PowerFullStop()
    {
        IntifaceService.FullStop = !IntifaceService.FullStop;
        Log("Full Stop: " + IntifaceService.FullStop, Severity.Normal);
    }

    public void ResetControlValues()
    {
        IntifaceService.PatUseRandomPower = false;
        IntifaceService.PatSpeedClimb = 2.0;
        IntifaceService.PatSpeedDrop = 3.0;
        IntifaceService.PatRandomOffTimeMin = 0.5;
        IntifaceService.PatRandomOffTimeMax = 1.0;
        IntifaceService.PatRandomOnTimeMin = 0.5;
        IntifaceService.PatRandomOnTimeMax = 2.0;
        IntifaceService.PatRandomPowerMin = 0.1;
        IntifaceService.PatRandomPowerMax = 1.0;
        IntifaceService.PatternPowerMulti = 0.2;
        InvokeAsync(StateHasChanged);
    }


    public enum ControlPreset
    {
        Manual,
        Pulses,
        PulsesRandom,
        PulsesRandomOnOff,
        PulsesRandomOnOffLong,

        ConstantRandom,

        Waves,
        WavesRandom,
        WavesRandomOffTime,

        ClimbDrop,
        ClimbDropHoldLonger
    }

    public void ApplyControlPreset(ControlPreset preset)
    {
        switch (preset)
        {
            case ControlPreset.Manual:
                SetIntifaceVariables(PatternType.None, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, false, 0.0, 0.0, 0.0);
                break;

            case ControlPreset.Pulses:
                SetIntifaceVariables(PatternType.Pulse, 0.0, 0.0, 0.5, 1.0, 0.5, 1.0, false, 0.2, 1.0, 1.0);
                break;

            case ControlPreset.PulsesRandom:
                SetIntifaceVariables(PatternType.Pulse, 0.0, 0.0, 0.5, 1.5, 0.5, 1.0, true, 0.2, 1.0, 1.0);
                break;

            case ControlPreset.PulsesRandomOnOff:
                SetIntifaceVariables(PatternType.Pulse, 0.0, 0.0, 0.5, 3.0, 0.8, 1.5, false, 0.0, 1.0, 1.0);
                break;

            case ControlPreset.PulsesRandomOnOffLong:
                SetIntifaceVariables(PatternType.Pulse, 0.0, 0.0, 0.5, 15.0, 0.8, 1.5, false, 0.0, 1.0, 1.0);
                break;

            case ControlPreset.ConstantRandom:
                SetIntifaceVariables(PatternType.Pulse, 0.0, 0.0, 0.0, 0.0, 0.25, 1.0, true, 0.2, 1.0, 1.0);
                break;

            case ControlPreset.Waves:
                SetIntifaceVariables(PatternType.Wave, 2.0, 3.0, 0.0, 0.0, 0.5, 1.0, false, 0.2, 1.0, 1.0);
                break;

            case ControlPreset.WavesRandom:
                SetIntifaceVariables(PatternType.Wave, 2.0, 3.0, 0.0, 0.5, 0.5, 2.0, true, 0.2, 1.0, 1.0);
                break;

            case ControlPreset.WavesRandomOffTime:
                SetIntifaceVariables(PatternType.Wave, 2.0, 3.0, 0.1, 3.0, 0.5, 3.0, false, 0.0, 1.0, 1.0);
                break;

            case ControlPreset.ClimbDrop:
                SetIntifaceVariables(PatternType.Wave, 0.3, 6.0, 0.2, 1.0, 0.5, 1.0, false, 0.0, 1.0, 1.0);
                break;

            case ControlPreset.ClimbDropHoldLonger:
                SetIntifaceVariables(PatternType.Wave, 0.3, 6.0, 0.4, 4.0, 1.5, 4.0, false, 0.0, 1.0, 1.0);
                break;
        }
    }

    public void SetIntifaceVariables(PatternType PatternType, double PatSpeedClimb, double PatSpeedDrop, double PatRandomOffTimeMin, double PatRandomOffTimeMax, double PatRandomOnTimeMin, double PatRandomOnTimeMax, bool PatUseRandomPower, double PatRandomPowerMin, double PatRandomPowerMax, double PowerInput)
    {
        IntifaceService.PatternType = PatternType;
        IntifaceService.PatSpeedClimb = PatSpeedClimb;
        IntifaceService.PatSpeedDrop = PatSpeedDrop;
        IntifaceService.PatRandomOffTimeMin = PatRandomOffTimeMin;
        IntifaceService.PatRandomOffTimeMax = PatRandomOffTimeMax;
        IntifaceService.PatRandomOnTimeMin = PatRandomOnTimeMin;
        IntifaceService.PatRandomOnTimeMax = PatRandomOnTimeMax;
        IntifaceService.PatUseRandomPower = PatUseRandomPower;
        IntifaceService.PatRandomPowerMin = PatRandomPowerMin;
        IntifaceService.PatRandomPowerMax = PatRandomPowerMax;
        IntifaceService.PatternPowerMulti = PowerInput;

        if (PatternType == PatternType.None)
        {
            IntifaceService.PatternsEnabled = false;
        }
        else
        {
            IntifaceService.PatternsEnabled = true;
        }

        LogService.AddLog(pageName, user, "Control Preset Applied", Severity.Normal);
        InvokeAsync(StateHasChanged);
    }
}