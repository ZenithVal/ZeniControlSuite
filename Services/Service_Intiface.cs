using System.Text.Json;
using Buttplug.Client;
using CoreOSC;
using MudBlazor;
using ZeniControlSuite.Extensions;
using ZeniControlSuite.Models;
using static MudBlazor.CategoryTypes;

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
        IntifaceClient.DeviceAdded += HandleDeviceAdded;
        IntifaceClient.DeviceRemoved += HandleDeviceRemoved;
    }
    public void Dispose()
    {
        IntifaceClient.Dispose();
        OSCService.OnOscMessageReceived -= HandleOSCMessage;
        IntifaceClient.DeviceAdded -= HandleDeviceAdded;
        IntifaceClient.DeviceRemoved -= HandleDeviceRemoved;
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

    public delegate void RequestGraphUpdate();
    public event RequestGraphUpdate? OnIntifaceGraphUpdate;

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
    private ButtplugClient IntifaceClient = new ButtplugClient("ZeniControlSuite");
    private string IntifaceServerAddress { get; set; } = "ws://localhost:16261";
    public bool IntifaceEnabled { get; set; } = false;
    public bool IntifaceRunning { get; set; } = false;
    public bool IntifaceConnected { get; private set; } = false;
    public bool DeviceConnected { get; private set; } = false;
    public int ConnectedDeviceCount => IntifaceClient.Devices.Length;
    public List<IntifaceDevice> ConfigedDevices { get; set; } = new List<IntifaceDevice>();

    public double PowerOutput { get; set; } = 0.0;
    public double PowerOutputPrevious { get; set; } = 0.0;

    public double PowerSpike = 0.0;
    public Dictionary<int, double> PowerSpikes = new Dictionary<int, double>();

    public bool FullStop { get; set; } = false;

    public bool OSCEnabled { get; set; } = false;

    private Parameter OutputParam = new Parameter("/ZCS_Intiface_Output", ParameterType.Float, 0.0f);

    public bool ControlEnabled { get; set; } = true;
    #endregion


    //==================//
    #region Haptic Settings
    public bool HapticsEnabled { get; set; } = false;
    public double HapticPower { get; set; } = 0.0;
    public double HapticMultiplier { get; set; } = 1.0;
    private bool HapticCalcRunning { get; set; } = false;
    public List<HapticInput> HapticInputs { get; set; } = new List<HapticInput>();
    private Dictionary<string, Parameter> HapticParameters = new Dictionary<string, Parameter>();
    #endregion


    //==================//
    #region Pattern Settings

    public bool PatternsEnabled { get; set; } = true;
    private bool PatternRunning { get; set; } = false;
    public bool PatUseRandomPower { get; set; } = false;


    public bool IntifacePointsEnabled { get; set; } = false;
    public bool PatternPointsUnlocked { get; set; } = false;

    public double PatternPower = 0.0;
    public double PatternPowerMulti { get; set; } = 0.0;

	public double PatternExponent = 1.0;

	public double PatternPowerPointMulti = 0.25;

    public int PatternIndex {
        get { return (int)PatternType; }
        set { PatternType = (PatternType)value; }
    }
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

        OutputParam.Address = "/avatar/parameters/" + config.GetProperty("OSCOutput").GetString();


        var devices = config.GetProperty("Devices");
        foreach (var device in devices.EnumerateArray())
        {
            ConfigedDevices.Add(DeserializeDevice(device));
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


    private IntifaceDevice DeserializeDevice(JsonElement _device)
    {
        ValidationLog($"Deserializing Device {_device.GetProperty("Name").GetString()}");

        var device = new IntifaceDevice();
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
	public bool DeviceScanning { get; private set; } = false;
	public async Task StartScanning()
    {
        DeviceScanning = true;
        InvokeControlUpdate();
        Log("Scanning for devices", Severity.Info);
        IntifaceClient.StartScanningAsync();
    }

    public async Task StopScanning()
    {
        DeviceScanning = false;
        InvokeControlUpdate();
        Log("Stopping device scan", Severity.Info);
        IntifaceClient.StopScanningAsync();
    }

    private void HandleDeviceAdded(object? _, DeviceAddedEventArgs aArgs)
    {
        Log("Device Added: " + aArgs.Device.Name, Severity.Info);
        if (!ConfigedDevices.Exists(x => x.Name == aArgs.Device.Name))
        {
            ConfigedDevices.Add(new IntifaceDevice() { Name = aArgs.Device.Name, DisplayName = aArgs.Device.Name, Enabled = true, Connected = true });
        }
        else
        {
            ConfigedDevices.Find(x => x.Name == aArgs.Device.Name).Connected = true;
        }

        DevicePowerSpike($"{aArgs.Device.Name} Connected", 0.0, 0.3, 600);

		InvokeControlUpdate();

/*        if (!scannerTimeoutRunning)
        {
			ScannerTimeout();
		}*/
	}

/*    bool scannerTimeoutRunning = false;
    private async Task ScannerTimeout()
    {
        scannerTimeoutRunning = true;
		await Task.Delay(60000);
		if (DeviceScanning)
        {
            StopScanning();
            scannerTimeoutRunning = false;
		}
	}*/

    private void HandleDeviceRemoved(object? _, DeviceRemovedEventArgs aArgs)
    {
        Log("Device Removed: " + aArgs.Device.Name, Severity.Info);

        if (ConfigedDevices.Exists(x => x.Name == aArgs.Device.Name))
        {
            ConfigedDevices.Find(x => x.Name == aArgs.Device.Name).Connected = false;
        }

/*        if (ConnectedDeviceCount < 1)
        {
            DeviceConnected = false;
            if (!DeviceScanning)
            {
                StartScanning();
            }
		}*/
    }

    #endregion


    //===========================================//
    #region Intiface Running
    public async Task IntifaceStart()
    {
        if (!IntifaceEnabled)
        {
            Log("Intiface not enabled, skipped start request", Severity.Info);
            return;
        }
        else if (IntifaceRunning)
        {
            Log("Intiface already running, skipped start request", Severity.Warning);
            return;
        }

        try
        {
            IntifaceRunning = true;
            InvokeControlUpdate();
            Log("Intiface Starting", Severity.Info);
            await IntifaceClient.ConnectAsync(new ButtplugWebsocketConnector(new Uri(IntifaceServerAddress)));
            await StartScanning();

            IntifaceConnected = true;

            Log("Intiface Connected", Severity.Info);
            DeviceHeartbeatTimer();
        }
        catch (Exception e)
        {
            Log("Intiface failed: " + e.Message, Severity.Error);
            IntifaceRunning = false;
            IntifaceConnected = false;
        }

		InvokeControlUpdate();
    }

    public async Task IntifaceStop()
    {
        if (!IntifaceRunning)
        {
            Log("Intiface not running, skipped stop request", Severity.Warning);
            return;
        }

        try
        {
            Log("Intiface Stopping", Severity.Info);
            await IntifaceClient.DisconnectAsync();
            IntifaceRunning = false;
            IntifaceConnected = false;
            if (deviceLoopTimer != null)
            {
                deviceLoopTimer.Stop();
                deviceLoopTimer.Dispose();
            }
            Log("Intiface Stopped", Severity.Info);
        }
        catch (Exception e)
        {
            Log("Intiface Stop Failed: " + e.Message, Severity.Error);
        }
        InvokeControlUpdate();
    }

    private void IntifaceClient_Disconnected(object? sender, EventArgs e)
    {
        Log("Intiface Disconnected", Severity.Info);
        IntifaceConnected = false;
        InvokeControlUpdate();
    }

    private System.Timers.Timer deviceLoopTimer;
    private void DeviceHeartbeatTimer() //Runs Device control loop
    {
        deviceLoopTimer = new System.Timers.Timer(100);
        deviceLoopTimer.Elapsed += async (sender, e) => await DeviceLoop();
        deviceLoopTimer.AutoReset = true;
        deviceLoopTimer.Enabled = true;
    }

    private async Task DeviceLoop()
    {
        if (FullStop)
        {
            PowerOutput = 0.0;
        }
        else
        {
            if (PatternsEnabled && !PatternRunning)
            {
                PatternRunning = true;
                DevicePattern();
            }
            else if (!PatternsEnabled)
            {
                PatternPower = 1.0;
            }

            if (PowerSpikes.Count != 0)
            {
                foreach (var spike in PowerSpikes) //is if the spike gets removed while this is running it'll break. Shouldn't happen... right?
                {
                    PowerSpike += spike.Value;
                }
            }
            else
            {
                PowerSpike = 0.0;
            }

            if (HapticsEnabled && !HapticCalcRunning)
            {
				HapticCalcRunning = true;
				IntifaceHapticCalc();
            }
            PowerOutput = Math.Clamp(Math.Pow((PatternPower * PatternPowerMulti), PatternExponent) + PowerSpike + HapticPower, 0.0, 1.0);
            if (IntifacePointsEnabled)
            {
                PowerOutput *= PatternPowerPointMulti;
            }
        }

        await DeviceControl();
    }

    public async Task DeviceControl()
    {
        foreach (var device in IntifaceClient.Devices)
        {
            /*if (ConfigedDevices.Find(x => x.Name == device.Name).Enabled == false)
			{
				continue;
			}*/

            await device.VibrateAsync(PowerOutput);
            if (TriggerWithinTolerance(PowerOutput - PowerOutputPrevious))
            {
                PowerOutputPrevious = PowerOutput;
                if (OSCEnabled)
                {
                    OutputParam.Value = (float)PowerOutput;
                    OSCService.sendOSCParameter(OutputParam);
                }
                if (HapticsEnabled)
                {
                    InvokeHapticsUpdate();
                }
                InvokeReadoutUpdate();
            }
		}
    }

    private void HandleOSCMessage(OscMessage messageReceived)
    {
        //Console.WriteLine($"OSC Message Received: {messageReceived.Address} {messageReceived.Arguments[0]}");
        if (HapticParameters.ContainsKey(messageReceived.Address))
        {
            var parameter = HapticParameters[messageReceived.Address];
            float value = OSCExtensions.FormatIncoming(messageReceived.Arguments[0], parameter.Type);
            HandleHapticParam(parameter, value);
        }
    }
    #endregion


    //===========================================//
    #region Patterns
    private async Task DevicePattern()
    {
        PatState = 0;

        if (PatUseRandomPower)
        {
            PatPowerGoal = new Random().NextDouble() * (PatRandomPowerMax - PatRandomPowerMin) + PatRandomPowerMin;
        }
        else
        {
            PatPowerGoal = PatternPowerMulti;
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
        HapticPower = newHapticPower * HapticMultiplier;
        HapticCalcRunning = false;
    }
    #endregion


    //===========================================//
    #region Power Spike
    private async void DevicePowerSpike(string source, double powerMin, double powerMax, int onTime,
        PatternType type = PatternType.None, int offTime = 100, int loops = 1, double speedClimb = 0.3, double speedDrop = 6.0)
    {
        Log(source + ": Power Spike", Severity.Info);
        int keyID = PowerSpikes.Count + 1;
        PowerSpikes.Add(keyID, 0.0);

        PatternState patState = PatternState.Up;

        for (int i = 0; i < loops; i++)
        {
            switch (type)
            {
                case PatternType.None:
                    PowerSpikes[keyID] = powerMax;
                    await Task.Delay(onTime);
                    break;

                case PatternType.Pulse:
                    PowerSpikes[keyID] = powerMax;
                    await Task.Delay(onTime);
                    PowerSpikes[keyID] = powerMin;
                    await Task.Delay(offTime);
                    break;

                case PatternType.Wave:
                    while (patState == PatternState.Up)
                    {
                        PowerSpikes[keyID] += (0.01 * speedClimb) * powerMax;

                        if (PowerSpikes[keyID] > 0.99 * powerMax)
                        {
                            PatState = PatternState.Down;
                        }
                        await Task.Delay(50);
                    }
                    PowerSpikes[keyID] = powerMax;
                    await Task.Delay(onTime);
                    while (patState == PatternState.Down)
                    {
                        PowerSpikes[keyID] -= (0.01 * speedDrop) * powerMax;

                        if (PowerSpikes[keyID] < 0.01 + powerMin)
                        {
                            PatState = PatternState.Up;
                        }
                        await Task.Delay(50);
                    }
                    PowerSpikes[keyID] = 0.0 + powerMin;
                    await Task.Delay(offTime);
                    break;
            }
        }

        PowerSpikes.Remove(keyID);
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