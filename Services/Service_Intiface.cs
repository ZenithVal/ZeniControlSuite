using Buttplug.Client;
using Buttplug.Client.Connectors.WebsocketConnector;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ZeniControlSuite.Models;
using ButtplugWebsocketConnector = Buttplug.Client.ButtplugWebsocketConnector;

namespace ZeniControlSuite.Services;

public class Service_Intiface : IHostedService, IDisposable
{
    [Inject] private Service_Logs LogService { get; set; } = default!;
    private void Log(string message, Severity severity)
    {
        LogService.AddLog("Service_Intiface", "System", message, severity, Variant.Outlined);
    }

    //===========================================//
    #region HostedService Stuff 
    public delegate void RequestControlUpdate();
    public event Service_Intiface.RequestControlUpdate? OnIntifaceControlsUpdate;

    public delegate void RequestReadoutUpdate();
    public event Service_Intiface.RequestReadoutUpdate? OnIntifaceReadoutUpdate;
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
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
    #endregion


    //===========================================//
    #region Settings
    private ButtplugClient _client = new ButtplugClient("ZeniControlSuite");

    public bool IntifaceEnabled { get; set; } = false;
    public bool IntifaceConnected { get; private set; } = false;
    public bool DeviceConnected { get; private set; } = false;


    public double PowerOutput { get; set; } = 0.0;
    public double PowerOutputPrevious { get; set; } = 0.0;
    public double PowerInput { get; set; } = 1.0;
    public double Power = 0.0;
    public double PowerSpike { get; set; } = 0.0;
    public bool FullStop { get; set; } = false;

    public bool UsePattern { get; set; } = false;
    public bool PatternRunning { get; private set; } = false;
    public bool PatUseRandomPower { get; set; } = false;

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

    public Service_Intiface(IServiceProvider services)
    {
        LogService = services.GetRequiredService<Service_Logs>();
        _client.DeviceAdded += HandleDeviceAdded;
        _client.DeviceRemoved += HandleDeviceRemoved;
    }
    public void Dispose()
    {
        _client.Dispose();
        _client.DeviceAdded -= HandleDeviceAdded;
        _client.DeviceRemoved -= HandleDeviceRemoved;
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
    #region Intiface Control
    public async Task IntifaceStart(Service_Logs logService)
    {
        if (IntifaceEnabled)
        {
            return;
        }
        try
        {
            Log("Intiface Starting", Severity.Info);
            await _client.ConnectAsync(new ButtplugWebsocketConnector(new Uri("ws://localhost:16261")));
            await ScanForDevices();
            IntifaceEnabled = true;

            Log("Intiface Connected", Severity.Info);
            DeviceHeartbeatTimer();

            InvokeControlUpdate();
        }
        catch (Exception e)
        {
            Log("Intiface failed: " + e.Message, Severity.Error);
            IntifaceEnabled = false;
            InvokeControlUpdate();
        }
    }

    private void DeviceHeartbeatTimer()
    {
        var timer = new System.Timers.Timer(100);
        timer.Elapsed += async (sender, e) => await DeviceLoop();
        timer.AutoReset = true;
        timer.Enabled = true;
    }

    private async Task DeviceLoop()
    {
        if (!PatternRunning)
        {
            PatternRunning = true;
            DevicePattern();
        }

        if (!FullStop)
        {
            PowerOutput = Math.Clamp((Power * PowerInput) + PowerSpike, 0.0, 1.0);
        }
        else
        {
            PowerOutput = 0.0;
        }

        await DeviceControl();
    }

    private async Task DevicePattern()
    {
        PatState = 0;
        PatPowerGoal = PowerInput;

        if (PatUseRandomPower)
        {
            PatPowerGoal = new Random().NextDouble() * (PatRandomPowerMax - PatRandomPowerMin) + PatRandomPowerMin;
        }

        Power = 0.0;
        switch (PatternType)
        {
            case PatternType.None:
                while (PatternType == PatternType.None)
                {
                    Power = PowerInput;
                    await Task.Delay(200);
                }
                break;

            case PatternType.Pulse:
                Power = PatPowerGoal;
                await DelayRandom(PatRandomOnTimeMin, PatRandomOnTimeMax);
                await Task.Delay(50);
                Power = PatRandomPowerMin;
                break;

            case PatternType.Wave:
                //states= 0: Up, 1: Down
                while (PatternType == PatternType.Wave && PatState == PatternState.Up)
                {
                    Power += (0.01 * PatSpeedClimb) * PatPowerGoal;

                    if (Power > 0.99 * PatPowerGoal)
                    {
                        PatState = PatternState.Down;

                    }
                    await Task.Delay(50);
                }
                Power = PatPowerGoal;
                await DelayRandom(PatRandomOnTimeMin, PatRandomOnTimeMax);

                while (PatternType == PatternType.Wave && PatState == PatternState.Down)
                {
                    Power -= (0.01 * PatSpeedDrop) * PatPowerGoal;

                    if (Power < 0.01 + PatRandomPowerMin)
                    {
                        PatState = PatternState.Up;
                    }

                    await Task.Delay(50);
                }
                Power = 0.0 + PatRandomPowerMin;

                break;

            default:
                break;
        }
        await DelayRandom(PatRandomOffTimeMin, PatRandomOffTimeMax);

        PatternRunning = false;
    }

    public async Task DeviceControl()
    {
        foreach (var device in _client.Devices)
        {
            await device.VibrateAsync(PowerOutput);
            if (TriggerWithinTolerance(PowerOutput - PowerOutputPrevious))
            {
                PowerOutputPrevious = PowerOutput;
                InvokeReadoutUpdate();
            }
        }
    }
    #endregion


    //===========================================//
    #region Idk yet
    public decimal CreateSineWave(double freq, double amplitude, double offset)
    {
        DateTime currentTime = DateTime.Now;
        var time = currentTime.TimeOfDay.TotalSeconds; // Current time in seconds
        return (decimal)((amplitude * Math.Sin(2 * Math.PI * freq * time)) + offset);
    }

    private async Task DelayRandom(double min, double max)
    {
        double delay = new Random().NextDouble() * (max - min) + min;
        await Task.Delay((int)(delay * 1000));
    }
    private bool TriggerWithinTolerance(double value, double tolerance = 0.03)
    {
        return value > tolerance || value < -tolerance;
    }
    #endregion



}