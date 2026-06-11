using System.Collections.Concurrent;
using System.Text.Json;
using Buttplug.Client;
using MudBlazor;
using ZeniControlSuite.Extensions;
using ZeniControlSuite.Models;

namespace ZeniControlSuite.Services;

public class Service_Intiface : IHostedService, IDisposable
{
    private readonly Service_Logs LogService;
    private readonly Service_OSC OSCService;
    private readonly object _historyLock = new();
    private readonly List<double> _powerHistory = new();
    private readonly int _historyLimit = 180;
    private ButtplugClient IntifaceClient = new("ZeniControlSuite");
    private System.Timers.Timer? deviceLoopTimer;
    private int _powerSpikeId;
    private bool _deviceLoopRunning;
    private string validationLog = string.Empty;

    public Service_Intiface(Service_Logs serviceLogs, Service_OSC serviceOSC)
    {
        LogService = serviceLogs;
        OSCService = serviceOSC;
        OSCService.OnOscMessageReceived += HandleOSCMessage;
        IntifaceClient.DeviceAdded += HandleDeviceAdded;
        IntifaceClient.DeviceRemoved += HandleDeviceRemoved;
    }

    public delegate void RequestControlUpdate();
    public event RequestControlUpdate? OnIntifaceControlsUpdate;

    public delegate void RequestReadoutUpdate();
    public event RequestReadoutUpdate? OnIntifaceReadoutUpdate;

    public delegate void RequestHapticsUpdate();
    public event RequestHapticsUpdate? OnIntifaceHapticsUpdate;

    public delegate void RequestGraphUpdate();
    public event RequestGraphUpdate? OnIntifaceGraphUpdate;

    public string IntifaceServerAddress { get; set; } = "ws://localhost:16261";
    public bool IntifaceEnabled { get; set; }
    public bool IntifaceRunning { get; set; }
    public bool IntifaceConnected { get; private set; }
    public bool DeviceConnected => ConnectedDeviceCount > 0;
    public int ConnectedDeviceCount => IntifaceClient.Devices.Length;
    public List<IntifaceDevice> ConfigedDevices { get; set; } = new();

    public double PowerOutput { get; set; }
    public double PowerOutputPrevious { get; set; }
    public double PowerSpike { get; set; }
    public ConcurrentDictionary<int, double> PowerSpikes { get; } = new();
    public bool FullStop { get; set; }
    public bool OSCEnabled { get; set; }
    private Parameter OutputParam { get; } = new("/avatar/parameters/ZCS_Intiface_Output", ParameterType.Float, 0.0f);
    public string OutputParameterName
    {
        get => StripAvatarPrefix(OutputParam.Address);
        set => OutputParam.Address = NormalizeAvatarParameter(value);
    }
    public bool ControlEnabled { get; set; } = true;

    public bool HapticsEnabled { get; set; }
    public double HapticPower { get; set; }
    public double HapticMultiplier { get; set; } = 1.0;
    private bool HapticCalcRunning { get; set; }
    public List<HapticInput> HapticInputs { get; set; } = new();
    private readonly Dictionary<string, Parameter> HapticParameters = new();

    public bool PatternsEnabled { get; set; } = false;
    private bool PatternRunning { get; set; }
    public bool PatUseRandomPower { get; set; }
    public bool IntifacePointsEnabled { get; set; }
    public bool PatternPointsUnlocked { get; set; }
    public double PatternPower { get; set; }
    public double PatternPowerMulti { get; set; } = 0.2;
    public double PatternExponent { get; set; } = 1.0;
    public double PatternPowerPointMulti { get; set; } = 0.25;
    public int PatternIndex
    {
        get => (int)PatternType;
        set => PatternType = (PatternType)value;
    }
    public PatternType PatternType { get; set; } = PatternType.None;
    public List<PatternType> GetPatternTypes => Enum.GetValues<PatternType>().ToList();
    public double PatSpeedClimb { get; set; } = 2.0;
    public double PatSpeedDrop { get; set; } = 3.0;
    public double PatRandomOffTimeMin { get; set; } = 0.5;
    public double PatRandomOffTimeMax { get; set; } = 2.0;
    public double PatRandomOnTimeMin { get; set; } = 0.5;
    public double PatRandomOnTimeMax { get; set; } = 2.0;
    public PatternState PatState { get; set; } = PatternState.Up;
    public double PatPowerGoal { get; set; }
    public double PatRandomPowerMin { get; set; } = 0.1;
    public double PatRandomPowerMax { get; set; } = 1.0;
    public int PatBurstCount { get; set; } = 3;
    public double PatBurstSpacing { get; set; } = 0.12;
    public double PatTremorRate { get; set; } = 9.0;
    public double PatTremorDepth { get; set; } = 0.45;
    public bool DeviceScanning { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        InitializeIntifaceConfig();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopDeviceLoopTimer();
        OSCService.OnOscMessageReceived -= HandleOSCMessage;
        IntifaceClient.DeviceAdded -= HandleDeviceAdded;
        IntifaceClient.DeviceRemoved -= HandleDeviceRemoved;
        IntifaceClient.Dispose();
    }

    private void Log(string message, Severity severity)
    {
        LogService.AddLog("Service_Intiface", "System", message, severity, Variant.Outlined);
    }

    public void InvokeIntifaceUpdate()
    {
        InvokeControlUpdate();
        InvokeReadoutUpdate();
        InvokeHapticsUpdate();
        InvokeGraphUpdate();
    }

    public void InvokeControlUpdate() => OnIntifaceControlsUpdate?.Invoke();
    public void InvokeReadoutUpdate() => OnIntifaceReadoutUpdate?.Invoke();
    public void InvokeHapticsUpdate() => OnIntifaceHapticsUpdate?.Invoke();
    public void InvokeGraphUpdate() => OnIntifaceGraphUpdate?.Invoke();

    public double[] GetPowerHistorySnapshot()
    {
        lock (_historyLock)
        {
            return _powerHistory.ToArray();
        }
    }

    private void RecordPowerHistory(double value)
    {
        lock (_historyLock)
        {
            _powerHistory.Add(Math.Clamp(value, 0.0, 1.0));
            if (_powerHistory.Count > _historyLimit)
            {
                _powerHistory.RemoveRange(0, _powerHistory.Count - _historyLimit);
            }
        }
    }

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
            ReadIntifaceJson(File.ReadAllText("Configs/Intiface.json"));
            Log("Service Started", Severity.Normal);
        }
        catch (Exception e)
        {
            Log($"Intiface.json parsing failed during {validationLog}: {e.Message}", Severity.Error);
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

        ValidationLog("reading Intiface config");
        ConfigedDevices.Clear();
        HapticInputs.Clear();
        HapticParameters.Clear();

        var config = JsonSerializer.Deserialize<JsonElement>(jsonString);
        IntifaceEnabled = GetBool(config, "IntifaceEnabled", false);
        IntifaceServerAddress = GetString(config, "IntifaceServer", IntifaceServerAddress);
        OSCEnabled = GetBool(config, "OSCEnabled", false);
        HapticsEnabled = GetBool(config, "HapticsEnabled", false);
        OutputParameterName = GetString(config, "OSCOutput", OutputParameterName);
        ControlEnabled = GetBool(config, "ControlEnabled", ControlEnabled);
        HapticMultiplier = GetDouble(config, "HapticMultiplier", HapticMultiplier);
        ReadPatternSettings(config);

        if (config.TryGetProperty("Devices", out var devices) && devices.ValueKind == JsonValueKind.Array)
        {
            foreach (var device in devices.EnumerateArray())
            {
                ConfigedDevices.Add(DeserializeDevice(device));
            }
        }

        if (config.TryGetProperty("HapticInputs", out var hapticsElement) && hapticsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var hapticElement in hapticsElement.EnumerateArray())
            {
                HapticInputs.Add(DeserializeHapticInput(hapticElement));
            }
        }

        RebuildHapticParameterIndex();
    }

    private void ReadPatternSettings(JsonElement config)
    {
        PatternPowerMulti = GetDouble(config, "PatternPowerMulti", PatternPowerMulti);
        PatternExponent = GetDouble(config, "PatternExponent", PatternExponent);
        PatternsEnabled = GetBool(config, "PatternsEnabled", PatternsEnabled);
        PatUseRandomPower = GetBool(config, "PatUseRandomPower", PatUseRandomPower);
        PatSpeedClimb = GetDouble(config, "PatSpeedClimb", PatSpeedClimb);
        PatSpeedDrop = GetDouble(config, "PatSpeedDrop", PatSpeedDrop);
        PatRandomOffTimeMin = GetDouble(config, "PatRandomOffTimeMin", PatRandomOffTimeMin);
        PatRandomOffTimeMax = GetDouble(config, "PatRandomOffTimeMax", PatRandomOffTimeMax);
        PatRandomOnTimeMin = GetDouble(config, "PatRandomOnTimeMin", PatRandomOnTimeMin);
        PatRandomOnTimeMax = GetDouble(config, "PatRandomOnTimeMax", PatRandomOnTimeMax);
        PatRandomPowerMin = GetDouble(config, "PatRandomPowerMin", PatRandomPowerMin);
        PatRandomPowerMax = GetDouble(config, "PatRandomPowerMax", PatRandomPowerMax);
        PatBurstCount = GetInt(config, "PatBurstCount", PatBurstCount);
        PatBurstSpacing = GetDouble(config, "PatBurstSpacing", PatBurstSpacing);
        PatTremorRate = GetDouble(config, "PatTremorRate", PatTremorRate);
        PatTremorDepth = GetDouble(config, "PatTremorDepth", PatTremorDepth);

        if (config.TryGetProperty("PatternType", out var rootPatternType) && rootPatternType.ValueKind == JsonValueKind.String)
        {
            PatternType = ParsePatternType(rootPatternType.GetString(), PatternType);
        }

        if (!config.TryGetProperty("PatternSettings", out var patternSettings) || patternSettings.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        PatternType = ParsePatternType(GetString(patternSettings, "PatternType", PatternType.ToString()), PatternType);
        PatternsEnabled = GetBool(patternSettings, "PatternsEnabled", PatternsEnabled);
        PatternPowerMulti = GetDouble(patternSettings, "PatternPowerMulti", PatternPowerMulti);
        PatternExponent = GetDouble(patternSettings, "PatternExponent", PatternExponent);
        PatUseRandomPower = GetBool(patternSettings, "PatUseRandomPower", PatUseRandomPower);
        PatSpeedClimb = GetDouble(patternSettings, "PatSpeedClimb", PatSpeedClimb);
        PatSpeedDrop = GetDouble(patternSettings, "PatSpeedDrop", PatSpeedDrop);
        PatRandomOffTimeMin = GetDouble(patternSettings, "PatRandomOffTimeMin", PatRandomOffTimeMin);
        PatRandomOffTimeMax = GetDouble(patternSettings, "PatRandomOffTimeMax", PatRandomOffTimeMax);
        PatRandomOnTimeMin = GetDouble(patternSettings, "PatRandomOnTimeMin", PatRandomOnTimeMin);
        PatRandomOnTimeMax = GetDouble(patternSettings, "PatRandomOnTimeMax", PatRandomOnTimeMax);
        PatRandomPowerMin = GetDouble(patternSettings, "PatRandomPowerMin", PatRandomPowerMin);
        PatRandomPowerMax = GetDouble(patternSettings, "PatRandomPowerMax", PatRandomPowerMax);
        PatBurstCount = GetInt(patternSettings, "PatBurstCount", PatBurstCount);
        PatBurstSpacing = GetDouble(patternSettings, "PatBurstSpacing", PatBurstSpacing);
        PatTremorRate = GetDouble(patternSettings, "PatTremorRate", PatTremorRate);
        PatTremorDepth = GetDouble(patternSettings, "PatTremorDepth", PatTremorDepth);
    }

    private IntifaceDevice DeserializeDevice(JsonElement deviceElement)
    {
        var name = GetString(deviceElement, "Name", string.Empty);
        ValidationLog($"Deserializing Device {name}");

        return new IntifaceDevice
        {
            Name = name,
            DisplayName = GetString(deviceElement, "DisplayName", name),
            Enabled = GetBool(deviceElement, "Enabled", true),
            Connected = false
        };
    }

    private HapticInput DeserializeHapticInput(JsonElement hapticElement)
    {
        validationLog = "Deserializing haptic input";
        var parameter = hapticElement.TryGetProperty("Parameter", out var parameterElement)
            ? DeserializeParameter(parameterElement)
            : new Parameter("/avatar/parameters/NewHapticInput", ParameterType.Float, 0f);

        ValidationLog($"Deserializing Haptic Input {parameter.Address}");

        float min = 0f;
        float max = 1f;
        if (hapticElement.TryGetProperty("MinMax", out var minMaxElement) && minMaxElement.ValueKind == JsonValueKind.Array)
        {
            var values = minMaxElement.EnumerateArray().ToList();
            if (values.Count > 0) min = values[0].GetSingle();
            if (values.Count > 1) max = values[1].GetSingle();
        }

        return new HapticInput
        {
            Enabled = GetBool(hapticElement, "Enabled", true),
            Parameter = parameter,
            Min = min,
            Max = max,
            Exponent = GetFloat(hapticElement, "Exponent", 1f),
            Multiplier = GetFloat(hapticElement, "Multiplier", 1f),
            Influence = GetFloat(hapticElement, "Influence", 1f)
        };
    }

    private Parameter DeserializeParameter(JsonElement parameterElement)
    {
        var parameter = new Parameter();

        if (parameterElement.ValueKind == JsonValueKind.Array)
        {
            var paramList = parameterElement.EnumerateArray().ToList();
            parameter.Address = NormalizeAvatarParameter(paramList.Count > 0 ? paramList[0].GetString() : string.Empty);
            parameter.Type = ParseParameterType(paramList.Count > 1 ? paramList[1].GetString() : "Float");
        }
        else if (parameterElement.ValueKind == JsonValueKind.Object)
        {
            parameter.Address = NormalizeAvatarParameter(GetString(parameterElement, "Address", GetString(parameterElement, "Name", string.Empty)));
            parameter.Type = ParseParameterType(GetString(parameterElement, "Type", "Float"));
        }
        else
        {
            parameter.Address = NormalizeAvatarParameter(parameterElement.GetString());
            parameter.Type = ParameterType.Float;
        }

        return parameter;
    }

    public async Task StartScanning()
    {
        if (!IntifaceConnected)
        {
            return;
        }

        try
        {
            DeviceScanning = true;
            InvokeControlUpdate();
            Log("Scanning for devices", Severity.Info);
            await IntifaceClient.StartScanningAsync();
        }
        catch (Exception ex)
        {
            DeviceScanning = false;
            Log("Device scan failed: " + ex.Message, Severity.Error);
            InvokeControlUpdate();
        }
    }

    public async Task StopScanning()
    {
        try
        {
            DeviceScanning = false;
            InvokeControlUpdate();
            Log("Stopping device scan", Severity.Info);
            await IntifaceClient.StopScanningAsync();
        }
        catch (Exception ex)
        {
            Log("Stopping device scan failed: " + ex.Message, Severity.Warning);
        }
    }

    private void HandleDeviceAdded(object? _, DeviceAddedEventArgs args)
    {
        Log("Device Added: " + args.Device.Name, Severity.Info);
        var configDevice = ConfigedDevices.FirstOrDefault(x => x.Name == args.Device.Name);
        if (configDevice == null)
        {
            ConfigedDevices.Add(new IntifaceDevice
            {
                Name = args.Device.Name,
                DisplayName = args.Device.Name,
                Enabled = true,
                Connected = true
            });
        }
        else
        {
            configDevice.Connected = true;
        }

        DevicePowerSpike($"{args.Device.Name} Connected", 0.0, 0.3, 600);
        InvokeControlUpdate();
    }

    private void HandleDeviceRemoved(object? _, DeviceRemovedEventArgs args)
    {
        Log("Device Removed: " + args.Device.Name, Severity.Info);

        var configDevice = ConfigedDevices.FirstOrDefault(x => x.Name == args.Device.Name);
        if (configDevice != null)
        {
            configDevice.Connected = false;
        }

        InvokeControlUpdate();
    }

    public async Task IntifaceStart()
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
            IntifaceRunning = true;
            InvokeControlUpdate();
            Log("Intiface Starting", Severity.Info);
            await IntifaceClient.ConnectAsync(new ButtplugWebsocketConnector(new Uri(IntifaceServerAddress)));
            IntifaceConnected = true;
            await StartScanning();
            Log("Intiface Connected", Severity.Info);
            DeviceHeartbeatTimer();
        }
        catch (Exception e)
        {
            Log("Intiface failed: " + e.Message, Severity.Error);
            IntifaceRunning = false;
            IntifaceConnected = false;
            DeviceScanning = false;
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
            IntifaceConnected = false;
            IntifaceRunning = false;
            DeviceScanning = false;
            StopDeviceLoopTimer();
            await StopAllDevices();
            await IntifaceClient.DisconnectAsync();
            Log("Intiface Stopped", Severity.Info);
        }
        catch (Exception e)
        {
            Log("Intiface Stop Failed: " + e.Message, Severity.Error);
            IntifaceConnected = false;
            IntifaceRunning = false;
            StopDeviceLoopTimer();
        }

        InvokeControlUpdate();
    }

    private void DeviceHeartbeatTimer()
    {
        StopDeviceLoopTimer();
        deviceLoopTimer = new System.Timers.Timer(100);
        deviceLoopTimer.Elapsed += async (_, _) => await DeviceLoop();
        deviceLoopTimer.AutoReset = true;
        deviceLoopTimer.Enabled = true;
    }

    private void StopDeviceLoopTimer()
    {
        if (deviceLoopTimer == null)
        {
            return;
        }

        deviceLoopTimer.Stop();
        deviceLoopTimer.Dispose();
        deviceLoopTimer = null;
    }

    private async Task DeviceLoop()
    {
        if (!IntifaceConnected || _deviceLoopRunning)
        {
            return;
        }

        _deviceLoopRunning = true;
        try
        {
            if (FullStop || !ControlEnabled)
            {
                PowerOutput = 0.0;
            }
            else
            {
                if (PatternsEnabled && !PatternRunning)
                {
                    PatternRunning = true;
                    _ = DevicePattern();
                }
                else if (!PatternsEnabled)
                {
                    PatternPower = 1.0;
                }

                PowerSpike = PowerSpikes.Values.ToArray().Sum();

                if (HapticsEnabled && !HapticCalcRunning)
                {
                    HapticCalcRunning = true;
                    await IntifaceHapticCalc();
                }

                PowerOutput = Math.Clamp(Math.Pow(PatternPower * PatternPowerMulti, PatternExponent) + PowerSpike + HapticPower, 0.0, 1.0);
                if (IntifacePointsEnabled)
                {
                    PowerOutput *= PatternPowerPointMulti;
                }
            }

            RecordPowerHistory(PowerOutput);
            InvokeGraphUpdate();
            await DeviceControl();
        }
        catch (Exception ex)
        {
            Log("Intiface device loop stopped after error: " + ex.Message, Severity.Warning);
            IntifaceConnected = false;
            IntifaceRunning = false;
            StopDeviceLoopTimer();
            InvokeControlUpdate();
        }
        finally
        {
            _deviceLoopRunning = false;
        }
    }

    public async Task DeviceControl()
    {
        if (!IntifaceConnected)
        {
            return;
        }

        foreach (var device in IntifaceClient.Devices.ToArray())
        {
            if (!IsDeviceEnabled(device.Name))
            {
                continue;
            }

            try
            {
                await device.RunOutputAsync(DeviceOutput.Vibrate.Percent(PowerOutput));
            }
            catch (Exception ex)
            {
                Log($"Device command failed for {device.Name}: {ex.Message}", Severity.Warning);
            }
        }

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

    private bool IsDeviceEnabled(string deviceName)
    {
        var configDevice = ConfigedDevices.FirstOrDefault(x => x.Name == deviceName);
        return configDevice?.Enabled ?? true;
    }

    private async Task StopAllDevices()
    {
        foreach (var device in IntifaceClient.Devices.ToArray())
        {
            try
            {
                await device.StopAsync();
            }
            catch { }
        }
    }

    private void HandleOSCMessage(OscMessage messageReceived)
    {
        if (messageReceived.Arguments.Count == 0)
        {
            return;
        }

        if (HapticParameters.TryGetValue(messageReceived.Address, out var parameter))
        {
            parameter.Value = OSCExtensions.FormatIncoming(messageReceived.Arguments[0], parameter.Type);
        }
    }

    private async Task DevicePattern()
    {
        PatState = PatternState.Up;
        PatPowerGoal = GetPatternGoal();
        PatternPower = Math.Clamp(PatRandomPowerMin, 0.0, 1.0);

        switch (PatternType)
        {
            case PatternType.None:
                while (PatternType == PatternType.None && PatternsEnabled && IntifaceConnected)
                {
                    PatternPower = 1.0;
                    await Task.Delay(200);
                }
                break;

            case PatternType.Pulse:
                PatternPower = PatPowerGoal;
                await DelayRandom(PatRandomOnTimeMin, PatRandomOnTimeMax);
                PatternPower = Math.Clamp(PatRandomPowerMin, 0.0, 1.0);
                break;

            case PatternType.RandomPulse:
                PatternPower = RandomBetween(PatRandomPowerMin, PatRandomPowerMax);
                await DelayRandom(PatRandomOnTimeMin, PatRandomOnTimeMax);
                PatternPower = Math.Clamp(PatRandomPowerMin, 0.0, 1.0);
                break;

            case PatternType.Wave:
                await RampPattern(Math.Clamp(PatRandomPowerMin, 0.0, 1.0), PatPowerGoal, PatSpeedClimb, PatternType.Wave);
                await DelayRandom(PatRandomOnTimeMin, PatRandomOnTimeMax);
                await RampPattern(PatPowerGoal, Math.Clamp(PatRandomPowerMin, 0.0, 1.0), PatSpeedDrop, PatternType.Wave);
                break;

            case PatternType.RampUp:
                await RampPattern(Math.Clamp(PatRandomPowerMin, 0.0, 1.0), PatPowerGoal, PatSpeedClimb, PatternType.RampUp);
                await DelayRandom(PatRandomOnTimeMin, PatRandomOnTimeMax);
                PatternPower = Math.Clamp(PatRandomPowerMin, 0.0, 1.0);
                break;

            case PatternType.RampDown:
                PatternPower = PatPowerGoal;
                await RampPattern(PatPowerGoal, Math.Clamp(PatRandomPowerMin, 0.0, 1.0), PatSpeedDrop, PatternType.RampDown);
                break;

            case PatternType.Saw:
                await RampPattern(Math.Clamp(PatRandomPowerMin, 0.0, 1.0), PatPowerGoal, PatSpeedClimb, PatternType.Saw);
                PatternPower = Math.Clamp(PatRandomPowerMin, 0.0, 1.0);
                break;

            case PatternType.Sine:
                await SinePattern(PatternType.Sine);
                break;

            case PatternType.Tremor:
                await TremorPattern();
                break;

            case PatternType.Burst:
                await BurstPattern();
                break;
        }

        await DelayRandom(PatRandomOffTimeMin, PatRandomOffTimeMax);
        PatternRunning = false;
    }

    private double GetPatternGoal()
    {
        return Math.Clamp(PatUseRandomPower
            ? RandomBetween(PatRandomPowerMin, PatRandomPowerMax)
            : Math.Max(PatRandomPowerMin, PatternPowerMulti), 0.0, 1.0);
    }

    private async Task RampPattern(double from, double to, double speed, PatternType expectedPattern)
    {
        const int frameDelayMs = 50;
        var safeSpeed = Math.Clamp(speed, 0.05, 40.0);
        var distance = Math.Abs(to - from);
        if (distance < 0.0001)
        {
            PatternPower = Math.Clamp(to, 0.0, 1.0);
            return;
        }

        var direction = to > from ? 1.0 : -1.0;
        PatternPower = Math.Clamp(from, 0.0, 1.0);
        while (PatternType == expectedPattern && PatternsEnabled && IntifaceConnected)
        {
            PatternPower = Math.Clamp(PatternPower + direction * 0.01 * safeSpeed, 0.0, 1.0);
            if ((direction > 0 && PatternPower >= to) || (direction < 0 && PatternPower <= to))
            {
                PatternPower = Math.Clamp(to, 0.0, 1.0);
                break;
            }
            await Task.Delay(frameDelayMs);
        }
    }

    private async Task SinePattern(PatternType expectedPattern)
    {
        const int frameDelayMs = 35;
        var durationMs = (int)(RandomBetween(PatRandomOnTimeMin, PatRandomOnTimeMax) * 1000);
        durationMs = Math.Clamp(durationMs, 250, 30000);
        var min = Math.Clamp(PatRandomPowerMin, 0.0, 1.0);
        var max = Math.Clamp(PatPowerGoal, min, 1.0);
        var frequency = Math.Clamp(PatTremorRate / 6.0, 0.2, 8.0);
        var start = Environment.TickCount64;

        while (PatternType == expectedPattern && PatternsEnabled && IntifaceConnected && Environment.TickCount64 - start < durationMs)
        {
            var elapsed = (Environment.TickCount64 - start) / 1000.0;
            var wave = (Math.Sin(elapsed * (Math.PI * 2.0) * frequency) + 1.0) * 0.5;
            PatternPower = min + (max - min) * wave;
            await Task.Delay(frameDelayMs);
        }
    }

    private async Task TremorPattern()
    {
        const int frameDelayMs = 35;
        var durationMs = (int)(RandomBetween(PatRandomOnTimeMin, PatRandomOnTimeMax) * 1000);
        durationMs = Math.Clamp(durationMs, 250, 30000);
        var goal = PatPowerGoal;
        var min = Math.Clamp(goal - PatTremorDepth, 0.0, 1.0);
        var max = Math.Clamp(goal, min, 1.0);
        var rate = Math.Clamp(PatTremorRate, 1.0, 40.0);
        var start = Environment.TickCount64;

        while (PatternType == PatternType.Tremor && PatternsEnabled && IntifaceConnected && Environment.TickCount64 - start < durationMs)
        {
            var elapsed = (Environment.TickCount64 - start) / 1000.0;
            var wave = (Math.Sin(elapsed * (Math.PI * 2.0) * rate) + 1.0) * 0.5;
            PatternPower = min + (max - min) * wave;
            await Task.Delay(frameDelayMs);
        }
    }

    private async Task BurstPattern()
    {
        var min = Math.Clamp(PatRandomPowerMin, 0.0, 1.0);
        var max = PatPowerGoal;
        var count = Math.Clamp(PatBurstCount, 1, 20);
        var spacingMs = (int)(Math.Clamp(PatBurstSpacing, 0.03, 3.0) * 1000);
        var onMs = (int)(Math.Clamp(PatRandomOnTimeMin, 0.03, 3.0) * 1000);

        for (var i = 0; i < count && PatternType == PatternType.Burst && PatternsEnabled && IntifaceConnected; i++)
        {
            PatternPower = max;
            await Task.Delay(onMs);
            PatternPower = min;
            await Task.Delay(spacingMs);
        }
    }

    public Task IntifaceHapticCalc()
    {
        var newHapticPower = 0.0f;
        foreach (var input in HapticInputs.Where(x => x.Enabled))
        {
            var value = input.Parameter.Type switch
            {
                ParameterType.Bool => input.Parameter.Value * input.Multiplier * input.Influence,
                ParameterType.Int or ParameterType.Float => CalculateFloatInput(input),
                _ => 0f
            };
            newHapticPower += value;
        }

        HapticPower = Math.Clamp(newHapticPower * HapticMultiplier, 0.0, 1.0);
        HapticCalcRunning = false;
        return Task.CompletedTask;
    }

    private static float CalculateFloatInput(HapticInput input)
    {
        var range = input.Max - input.Min;
        if (Math.Abs(range) < 0.0001f)
        {
            return 0f;
        }

        var normalized = Math.Clamp((input.Parameter.Value - input.Min) / range, 0f, 1f);
        normalized = (float)Math.Pow(normalized, input.Exponent);
        return normalized * input.Multiplier * input.Influence;
    }

    private async void DevicePowerSpike(string source, double powerMin, double powerMax, int onTime,
        PatternType type = PatternType.None, int offTime = 100, int loops = 1, double speedClimb = 0.3, double speedDrop = 6.0)
    {
        Log(source + ": Power Spike", Severity.Info);
        int keyID = Interlocked.Increment(ref _powerSpikeId);
        PowerSpikes[keyID] = 0.0;

        try
        {
            for (int i = 0; i < loops; i++)
            {
                var patState = PatternState.Up;
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
                                patState = PatternState.Down;
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
                                patState = PatternState.Up;
                            }
                            await Task.Delay(50);
                        }

                        PowerSpikes[keyID] = powerMin;
                        await Task.Delay(offTime);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Power spike failed: {ex.Message}", Severity.Warning);
        }
        finally
        {
            PowerSpikes.TryRemove(keyID, out _);
        }
    }

    public void AddConfiguredDevice()
    {
        ConfigedDevices.Add(new IntifaceDevice
        {
            Name = "New Device",
            DisplayName = "New Device",
            Enabled = true
        });
        InvokeControlUpdate();
    }

    public void RemoveConfiguredDevice(IntifaceDevice device)
    {
        ConfigedDevices.Remove(device);
        InvokeControlUpdate();
    }

    public void AddHapticInput()
    {
        var input = new HapticInput
        {
            Enabled = true,
            Parameter = new Parameter("/avatar/parameters/NewHapticInput", ParameterType.Float, 0f),
            Min = 0f,
            Max = 1f,
            Exponent = 1f,
            Multiplier = 1f,
            Influence = 1f
        };
        HapticInputs.Add(input);
        RebuildHapticParameterIndex();
        InvokeControlUpdate();
    }

    public void RemoveHapticInput(HapticInput input)
    {
        HapticInputs.Remove(input);
        RebuildHapticParameterIndex();
        InvokeControlUpdate();
    }

    public void RebuildHapticParameterIndex()
    {
        HapticParameters.Clear();
        foreach (var input in HapticInputs)
        {
            input.Parameter.Address = NormalizeAvatarParameter(input.Parameter.Address);
            HapticParameters[input.Parameter.Address] = input.Parameter;
        }
    }

    public void SaveConfig()
    {
        try
        {
            RebuildHapticParameterIndex();
            Directory.CreateDirectory("Configs");
            var config = new
            {
                IntifaceEnabled,
                IntifaceServer = IntifaceServerAddress,
                Devices = ConfigedDevices.Select(device => new
                {
                    device.Name,
                    device.DisplayName,
                    device.Enabled
                }).ToArray(),
                OSCEnabled,
                OSCOutput = OutputParameterName,
                ControlEnabled,
                HapticsEnabled,
                HapticMultiplier,
                PatternSettings = new
                {
                    PatternType = PatternType.ToString(),
                    PatternsEnabled,
                    PatternPowerMulti,
                    PatternExponent,
                    PatUseRandomPower,
                    PatSpeedClimb,
                    PatSpeedDrop,
                    PatRandomOffTimeMin,
                    PatRandomOffTimeMax,
                    PatRandomOnTimeMin,
                    PatRandomOnTimeMax,
                    PatRandomPowerMin,
                    PatRandomPowerMax,
                    PatBurstCount,
                    PatBurstSpacing,
                    PatTremorRate,
                    PatTremorDepth
                },
                HapticInputs = HapticInputs.Select(input => new
                {
                    input.Enabled,
                    Parameter = new object[] { StripAvatarPrefix(input.Parameter.Address), input.Parameter.Type.ToString() },
                    MinMax = new[] { input.Min, input.Max },
                    input.Exponent,
                    input.Multiplier,
                    input.Influence
                }).ToArray()
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("Configs/Intiface.json", json);
            Log("Intiface config saved", Severity.Success);
            InvokeIntifaceUpdate();
        }
        catch (Exception ex)
        {
            Log("Intiface config save failed: " + ex.Message, Severity.Error);
        }
    }

    public static string NormalizeAvatarParameter(string? value)
    {
        var parameter = (value ?? string.Empty).Trim();
        if (parameter.StartsWith("/avatar/parameters/", StringComparison.OrdinalIgnoreCase))
        {
            return parameter;
        }
        parameter = parameter.TrimStart('/');
        return "/avatar/parameters/" + parameter;
    }

    public static string StripAvatarPrefix(string? value)
    {
        var parameter = (value ?? string.Empty).Trim();
        const string prefix = "/avatar/parameters/";
        return parameter.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? parameter[prefix.Length..]
            : parameter.TrimStart('/');
    }

    private static ParameterType ParseParameterType(string? type)
    {
        return Enum.TryParse<ParameterType>(type, ignoreCase: true, out var parsed) ? parsed : ParameterType.Float;
    }

    private static string GetString(JsonElement element, string propertyName, string defaultValue)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? defaultValue
            : defaultValue;
    }

    private static bool GetBool(JsonElement element, string propertyName, bool defaultValue)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : defaultValue;
    }

    private static float GetFloat(JsonElement element, string propertyName, float defaultValue)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetSingle()
            : defaultValue;
    }

    private static double GetDouble(JsonElement element, string propertyName, double defaultValue)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetDouble()
            : defaultValue;
    }

    private static int GetInt(JsonElement element, string propertyName, int defaultValue)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : defaultValue;
    }

    private static PatternType ParsePatternType(string? value, PatternType defaultValue)
    {
        return Enum.TryParse<PatternType>(value, ignoreCase: true, out var parsed) ? parsed : defaultValue;
    }

    private static double RandomBetween(double min, double max)
    {
        var safeMin = Math.Max(0, Math.Min(min, max));
        var safeMax = Math.Max(safeMin, Math.Max(min, max));
        return Random.Shared.NextDouble() * (safeMax - safeMin) + safeMin;
    }

    private static async Task DelayRandom(double min, double max)
    {
        double delay = RandomBetween(min, max);
        await Task.Delay((int)(delay * 1000));
    }

    private static bool TriggerWithinTolerance(double value, double tolerance = 0.03)
    {
        return value > tolerance || value < -tolerance;
    }
}
