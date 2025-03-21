﻿using System.ComponentModel;
using System.Text.Json;
using Buttplug.Client;
using CoreOSC;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ZeniControlSuite.Extensions;
using ZeniControlSuite.Models;
using ZeniControlSuite.Models.Intiface;

namespace ZeniControlSuite.Services;

public class Service_Intiface : IHostedService, IDisposable
{
	private readonly Service_Logs LogService;
	private readonly Service_OSC OSCService;

	public Service_Intiface(Service_Logs serviceLogs, Service_OSC serviceOSC)
	{
		LogService = serviceLogs;
		OSCService = serviceOSC;
		OSCService.OnOscMessageReceived += HandleOSCMessage;
		_client.DeviceAdded += HandleDeviceAdded;
		_client.DeviceRemoved += HandleDeviceRemoved;
	}
	public void Dispose()
	{
		_client.Dispose();
		OSCService.OnOscMessageReceived -= HandleOSCMessage;
		_client.DeviceAdded -= HandleDeviceAdded;
		_client.DeviceRemoved -= HandleDeviceRemoved;
	}

	private void Log(string message, Severity severity)
    {
        LogService.AddLog("Service_Intiface", "System", message, severity, Variant.Outlined);
    }

    //===========================================//
    #region HostedService Stuff 
    public delegate void RequestControlUpdate();
    public event RequestControlUpdate? OnIntifaceControlsUpdate;

    public delegate void RequestReadoutUpdate();
    public event RequestReadoutUpdate? OnIntifaceReadoutUpdate;

    public delegate void RequestHapticsUpdate();
    public event RequestHapticsUpdate? OnIntifaceHapticsUpdate;

	public Task StartAsync(CancellationToken cancellationToken)
    {
        InitializeIntifaceConfig();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

	public void InvokeIntifaceUpdate()
    {
        InvokeControlUpdate();
        InvokeReadoutUpdate();
        InvokeHapticsUpdate();
	}

    public void InvokeControlUpdate()
    {
        if (OnIntifaceControlsUpdate != null)
        {
            OnIntifaceControlsUpdate?.Invoke();
        }
    }

    public void InvokeReadoutUpdate()
    {
        if (OnIntifaceReadoutUpdate != null)
        {
            OnIntifaceReadoutUpdate?.Invoke();
        }
    }

    public void InvokeHapticsUpdate()
    {
		if (OnIntifaceHapticsUpdate != null)
        {
			OnIntifaceHapticsUpdate?.Invoke();
		}
	}
	#endregion


	//===========================================//
	#region Main Settings
	private ButtplugClient _client = new ButtplugClient("ZeniControlSuite");

    private string IntifaceServerAddress { get; set; } = "ws://localhost:16261";
    public bool IntifaceEnabled { get; set; } = false;
    public bool IntifaceRunning { get; set; } = false;
	public bool IntifaceConnected { get; private set; } = false;
    public bool DeviceConnected { get; private set; } = false;
    public List <Device> Devices { get; set; } = new List<Device>();

    public double PowerOutput { get; set; } = 0.0;
    public double PowerOutputPrevious { get; set; } = 0.0;
    public double PowerSpike { get; set; } = 0.0;
    public bool FullStop { get; set; } = false;

    public bool OSCEnabled { get; set; } = false;

    private Parameter OutputParam = new Parameter("/ZCS_Intiface_Output", ParameterType.Float, 0.0f);
	#endregion


	//==================//
	#region Haptic Settings
	public bool HapticsEnabled { get; set; } = false;
	public double HapticPower { get; set; } = 0.0;
    private bool HapticCalcRunning { get; set; } = false;
    public List<HapticInput> HapticInputs { get; set; } = new List<HapticInput>();
    private Dictionary<string, Parameter> HapticParameters = new Dictionary<string, Parameter>();
	#endregion


	//==================//
	#region Pattern Settings

	public bool UsePattern { get; set; } = false;
	private bool PatternRunning { get; set; } = false;
	public bool PatUseRandomPower { get; set; } = false;


	public double PatternPower = 0.0;
	public double PatternPowerMulti { get; set; } = 1.0;

	public PatternType PatternType { get; set; } = PatternType.Wave;
	public List<PatternType> GetPatternTypes => Enum.GetValues<PatternType>().ToList();

	public double PatSpeedClimb = 2.0;
	public double PatSpeedDrop = 3.0;

	public double PatRandomOffTimeMin = 0.5; //time in seconds to wait before turning on
	public double PatRandomOffTimeMax = 2.0;
	public double PatRandomOnTimeMin = 0.5; //time in seconds to wait before turning off
	public double PatRandomOnTimeMax = 2.0;

	public PatternState PatState = PatternState.Up;
	public double PatPowerGoal = 0.0;
	public double PatRandomPowerMin = 0.1;
	public double PatRandomPowerMax = 1.0;
	#endregion


	//===========================================//
	#region Intialization
	private string validationLog = "";
    private void ValidationLog(string message)
    {
		validationLog = message;
		Console.WriteLine($"Intiface | {validationLog}");
	}

	private void InitializeIntifaceConfig()
    {
		if (!File.Exists("Configs/Intiface.json"))
		{
			Log("Intiface config file not found.", Severity.Info);
            IntifaceEnabled = false;
            return;
		}

		try
		{
			var jsonString = File.ReadAllText("Configs/Intiface.json");
			ReadIntifaceJson(jsonString);
			Log("Service Started", Severity.Normal);
			Console.WriteLine("");
		}
		catch (Exception e)
		{
			Log($"AvatarControls.json parsing failed during {validationLog}", Severity.Error);
			Console.WriteLine(e.Message);
			IntifaceEnabled = false;
		}

		InvokeIntifaceUpdate();
	}

    private void ReadIntifaceJson(string jsonString)
    {
        if (IntifaceRunning)
        {
			return;
		}

        ValidationLog("reading Intiface Core Config");
        
        var config = JsonSerializer.Deserialize<JsonElement>(jsonString);
        IntifaceEnabled = config.GetProperty("IntifaceEnabled").GetBoolean();
		IntifaceServerAddress = config.GetProperty("IntifaceServer").GetString();

        OSCEnabled = config.GetProperty("OSCEnabled").GetBoolean();
        HapticsEnabled = config.GetProperty("HapticsEnabled").GetBoolean();

        var devices = config.GetProperty("Devices");
        foreach (var device in devices.EnumerateArray())
        {
            Devices.Add(DeserializeDevice(device));
        }

		if (HapticsEnabled)
        {
			var hapticsElement = config.GetProperty("HapticInputs");
            foreach (var hapticElement in hapticsElement.EnumerateArray())
            {
				HapticInput hapticInput = DeserializeHapticInput(hapticElement);
				HapticInputs.Add(hapticInput);
			}
        }
    }


	private Device DeserializeDevice(JsonElement _device)
	{
        ValidationLog($"Deserializing Device {_device.GetProperty("Name").GetString()}");

        var device = new Device();
		device.Name = _device.GetProperty("Name").GetString();
		device.DisplayName = _device.GetProperty("DisplayName").GetString();
		device.Enabled = _device.GetProperty("Enabled").GetBoolean();
        device.Connected = false;

		return device;
	}

	private HapticInput DeserializeHapticInput(JsonElement _hapticInput)
	{
		validationLog = $"Deserializing Parameter... ";
        //Gotta construct the param first
        var parameter = DeserializeParamter(_hapticInput.GetProperty("Parameter"));
		ValidationLog($"Deserializing Haptic Input {parameter.Address}");

		var minMax = _hapticInput.GetProperty("MinMax").EnumerateArray().ToList();

        float min = minMax[0].GetSingle();
        float max = minMax[1].GetSingle();
        float exponent = _hapticInput.GetProperty("Exponent").GetSingle();
		float multiplier = _hapticInput.GetProperty("Multiplier").GetSingle();
		float influence = _hapticInput.GetProperty("Influence").GetSingle();

        var hapticInput = new HapticInput() {
			Parameter = parameter,
			Min = min,
			Max = max,
			Exponent = exponent,
			Multiplier = multiplier,
			Influence = influence
		};

        HapticParameters.Add(parameter.Address, parameter);

		return hapticInput;
	}

    private Parameter DeserializeParamter(JsonElement _parameter)
    {
        var paramList = _parameter.EnumerateArray().ToList();
		var parameter = new Parameter();

		parameter.Address = "/avatar/parameters/" + paramList[0].GetString();

		var type = paramList[1].GetString();
		if (type == "Bool")
		{
			parameter.Type = ParameterType.Bool;
		}
		else if (type == "Float")
		{
			parameter.Type = ParameterType.Float;
		}

		return parameter;
	}
	#endregion


	//===========================================//
	#region Device Scanning and Connection
	private async Task ScanForDevices()
    {
        Log("Scanning for devices", Severity.Info);
        await _client.StartScanningAsync();

        while (DeviceConnected == false)
        {
            await Task.Delay(500);
        }

        await _client.StopScanningAsync();
    }
    private void HandleDeviceAdded(object? _, DeviceAddedEventArgs aArgs)
    {
        Log("Device Added: " + aArgs.Device.Name, Severity.Info);
        DeviceConnected = true;
    }
    private void HandleDeviceRemoved(object? _, DeviceRemovedEventArgs aArgs)
    {
        Log("Device Removed: " + aArgs.Device.Name, Severity.Info);
        DeviceConnected = false;
    }
    #endregion


    //===========================================//
    #region Intiface 
    public async Task IntifaceStart(Service_Logs logService)
    {
        if (!IntifaceEnabled)
        {
            Log("Intiface not enabled, skipped start request", Severity.Info);
			return;
		}
        
        if (IntifaceRunning)
        {
            Log("Intiface already running, skipped start request", Severity.Warning);
            return;
        }

        try
        {
            Log("Intiface Starting", Severity.Info);
            await _client.ConnectAsync(new ButtplugWebsocketConnector(new Uri(IntifaceServerAddress)));
            await ScanForDevices();
            IntifaceRunning = true;

            Log("Intiface Connected", Severity.Info);
            DeviceHeartbeatTimer();

            InvokeControlUpdate();
        }
        catch (Exception e)
        {
            Log("Intiface failed: " + e.Message, Severity.Error);
            IntifaceRunning = false;
            InvokeControlUpdate();
        }
    }

    private void DeviceHeartbeatTimer()
    {
        var timer = new System.Timers.Timer(500);
        timer.Elapsed += async (sender, e) => await DeviceLoop();
        timer.AutoReset = true;
        timer.Enabled = true;
    }

	private async Task DeviceLoop()
    {
        if (UsePattern && !PatternRunning)
        {
            PatternRunning = true;
            DevicePattern();
        }
		else if (!UsePattern)
		{
			PatternPower = 1.0;
        }

		if (HapticsEnabled && !HapticCalcRunning)
		{
			IntifaceHapticCalc();
		}

        if (FullStop)
        {
			PowerOutput = 0.0;
		}
        else
        {
			PowerOutput = Math.Clamp((PatternPower * PatternPowerMulti) + PowerSpike + HapticPower, 0.0, 1.0);
        }

        await DeviceControl();
    }

    public async Task DeviceControl()
    {
        foreach (var device in _client.Devices)
        {
            await device.VibrateAsync(PowerOutput);
            if (TriggerWithinTolerance(PowerOutput - PowerOutputPrevious))
            {
                PowerOutputPrevious = PowerOutput;
				if (OSCEnabled) 
				{
					OutputParam.Value = (float)PowerOutput;
					OSCService.sendOSCParameter(OutputParam);
				}
                InvokeReadoutUpdate();
            }
        }
    }
	#endregion


	//===========================================//
	#region Patterns
	private async Task DevicePattern()
	{
		PatState = 0;
		PatPowerGoal = PatternPowerMulti;

		if (PatUseRandomPower)
		{
			PatPowerGoal = new Random().NextDouble() * (PatRandomPowerMax - PatRandomPowerMin) + PatRandomPowerMin;
		}

		PatternPower = 0.0;
		switch (PatternType)
		{
			case PatternType.None:
				while (PatternType == PatternType.None)
				{
					PatternPower = PatternPowerMulti;
					await Task.Delay(200);
				}
				break;

			case PatternType.Pulse:
				PatternPower = PatPowerGoal;
				await DelayRandom(PatRandomOnTimeMin, PatRandomOnTimeMax);
				await Task.Delay(50);
				PatternPower = PatRandomPowerMin;
				break;

			case PatternType.Wave:
				//states= 0: Up, 1: Down
				while (PatternType == PatternType.Wave && PatState == PatternState.Up)
				{
					PatternPower += (0.01 * PatSpeedClimb) * PatPowerGoal;

					if (PatternPower > 0.99 * PatPowerGoal)
					{
						PatState = PatternState.Down;

					}
					await Task.Delay(50);
				}
				PatternPower = PatPowerGoal;
				await DelayRandom(PatRandomOnTimeMin, PatRandomOnTimeMax);

				while (PatternType == PatternType.Wave && PatState == PatternState.Down)
				{
					PatternPower -= (0.01 * PatSpeedDrop) * PatPowerGoal;

					if (PatternPower < 0.01 + PatRandomPowerMin)
					{
						PatState = PatternState.Up;
					}

					await Task.Delay(50);
				}
				PatternPower = 0.0 + PatRandomPowerMin;

				break;

			default:
				break;
		}
		await DelayRandom(PatRandomOffTimeMin, PatRandomOffTimeMax);

		PatternRunning = false;
	}
	#endregion


	//===========================================//
	#region Haptics
	private void HandleOSCMessage(OscMessage messageReceived)
	{
        Console.WriteLine($"OSC Message Received: {messageReceived.Address} {messageReceived.Arguments[0]}");
		if (HapticParameters.ContainsKey(messageReceived.Address))
        {
			var parameter = HapticParameters[messageReceived.Address];
			float value = OSCExtensions.FormatIncoming(messageReceived.Arguments[0], parameter.Type);
            HandleHapticParam(parameter, value);
		}
	}
	private void HandleHapticParam(Parameter param, float value) //Used for incoming OSC messages. Updates the param in the app and invokes an update for visuals.
	{
		var hapticInput = HapticInputs.Find(x => x.Parameter.Address == param.Address);
		if (hapticInput != null)
		{
            hapticInput.Parameter.Value = value;
        }
	}

	public async Task IntifaceHapticCalc()
	{
		HapticCalcRunning = true;
        var newHapticPower = 0.0f;
        foreach (var HI in HapticInputs)
        {
			float value = 0.0f;
            switch (HI.Parameter.Type)
            {
				case ParameterType.Float:
                    //Min Max, then exponent, then multiplier, then influence
					value = (HI.Parameter.Value - HI.Min) / (HI.Max - HI.Min);
					value = (float)Math.Pow(value, HI.Exponent);
					value *= HI.Multiplier * HI.Influence;
					newHapticPower += value;
					break;
				case ParameterType.Bool:
					value = HI.Parameter.Value * HI.Multiplier * HI.Influence;
					newHapticPower += value;
					break;
			}
		}
        HapticPower = newHapticPower;
		HapticCalcRunning = false;
		InvokeHapticsUpdate();
	}
	#endregion


	//===========================================//
	#region Helpful
	private async Task DelayRandom(double min, double max)
    {
        double delay = new Random().NextDouble() * (max - min) + min;
        await Task.Delay((int)(delay * 1000));
    }

	//Change if the difference if bigger than the tolerance
    private bool TriggerWithinTolerance(double value, double tolerance = 0.03)
    {
        return value > tolerance || value < -tolerance;
    }
    #endregion



}