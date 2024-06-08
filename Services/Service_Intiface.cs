using Buttplug.Client;
using Buttplug.Client.Connectors.WebsocketConnector;
using MudBlazor;
using static MudBlazor.CategoryTypes;

namespace ZeniControlSuite.Services;

public class Service_Intiface: IHostedService, IDisposable
{
    public delegate void RequestDisplayUpdate();
    public event Service_Intiface.RequestDisplayUpdate? OnRequestDisplayUpdate;
    
    private readonly Service_Logs LogService;
    private ButtplugClient _client = new ButtplugClient("ZeniControlSuite");
    public const string ServiceName = "IntifaceService";
    public bool DeviceConnected { get; private set; } = false;
    public bool InterfaceConnected { get; private set; } = false;
    
    public bool EnableIntiface { get; set; } = false;
    
    public double PowerOutput { get; set; } = 0.0;
    public double PowerOutputPrevious { get; set; } = 0.0;
    public double PowerInput { get; set; } = 1.0;
    public double Power = 0.0;
    public double PowerSpike { get; set; } = 0.0;
    public bool FullStop { get; set; } = false;

    /*    
    public List<ChartSeries> powerHistory = new List<ChartSeries>()
    {
        new ChartSeries() { Name = "", Data = new double[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 } },
    };
    */

    public bool UsePattern { get; set; } = false;
    public bool PatternRunning { get; private set; } = false;
    public bool PatUseRandomPower { get; set; } = false;

    public PatternType PatternType { get; set; } = PatternType.Wave;
    public List<PatternType> GetPatternTypes => Enum.GetValues<PatternType>().ToList();
    
    public double PatSpeedClimb = 2.0;
    public double PatSpeedDrop = 2.0;
    
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

    private void HandleDeviceAdded(object? _, DeviceAddedEventArgs aArgs)
    {
        LogService.AddLog(ServiceName, "System", $"Device Added: {aArgs.Device.Name}", Severity.Info, Variant.Outlined);
        DeviceConnected = true;
    }
    private void HandleDeviceRemoved(object? _, DeviceRemovedEventArgs aArgs)
    {
        LogService.AddLog(ServiceName, "System", $"Device Removed: {aArgs.Device.Name}", Severity.Info, Variant.Outlined);
        DeviceConnected = false;
    }
    
    public async Task ScanForDevices()
    {
        await _client.StartScanningAsync();

        while (DeviceConnected == false)
        {
            await Task.Delay(50);
        }

        await _client.StopScanningAsync();
    }
    
    public async Task ControlDevice()
    {
        foreach (var device in _client.Devices)
        {
            await device.VibrateAsync(PowerOutput);
            if(TriggerWithinTolerance(PowerOutput - PowerOutputPrevious))
            {
                PowerOutputPrevious = PowerOutput;
                OnRequestDisplayUpdate?.Invoke();
            }
        }
    }
    
    private async Task DelayRandom(double min, double max)
    {
        double delay = new Random().NextDouble() * (max - min) + min;
        await Task.Delay((int)(delay * 1000));
    }


    private async Task RunPattern()
    {
        PatState = 0;
        PatPowerGoal = PatRandomPowerMax;

        if (PatUseRandomPower)
        {
            PatPowerGoal = new Random().NextDouble() * (PatRandomPowerMax - PatRandomPowerMin) + PatRandomPowerMin;
        }

        Power = 0.0;
        switch (PatternType)
        {
            case PatternType.Constant:
                Power = PatPowerGoal;
                await DelayRandom(PatRandomOnTimeMin, PatRandomOnTimeMax);
                await Task.Delay(50);
                break;

            case PatternType.Wave:
                //states= 0: Up, 1: Down
                while (PatState == PatternState.Up)
                {
                    Power += (0.01 * PatSpeedClimb) * PatPowerGoal;

                    if (Power > 0.99 * PatPowerGoal)
                    {
                        PatState = PatternState.Down;
                        Power = PatPowerGoal;
                    }

                    await Task.Delay(50);
                }

                await DelayRandom(PatRandomOnTimeMin, PatRandomOnTimeMax);
                while (PatState == PatternState.Down)
                {
                    Power -= (0.01 * PatSpeedDrop) * PatPowerGoal;

                    if (Power < 0.01 + PatRandomPowerMin)
                    {
                        PatState = PatternState.Up;
                        Power = 0.0 + PatRandomPowerMin;
                    }

                    await Task.Delay(50);
                }

                break;

            default:
                break;
        }
        await DelayRandom(PatRandomOffTimeMin, PatRandomOffTimeMax);

        PatternRunning = false;
    }

    public async Task IntifaceRunner(Service_Logs logService, CancellationToken stoppingToken)
    {
        try
        {
            await _client.ConnectAsync(new ButtplugWebsocketConnector(new Uri("ws://localhost:16261")));
            LogService.AddLog(ServiceName, "System", "Connected to Intiface client", Severity.Normal, Variant.Outlined);
            InterfaceConnected = true;
        }
        catch (Exception e)
        {
            LogService.AddLog(ServiceName, "System", $"Error connecting to Intiface client: {e.Message}", Severity.Error, Variant.Outlined);
            InterfaceConnected = false;
            EnableIntiface = false;
            return;
        }
        
        await ScanForDevices();
        
        
        while (EnableIntiface)
        {
            if (!PatternRunning)
            {
                PatternRunning = true;
                RunPattern();
            }

            if (!FullStop)
            {
                PowerOutput = Math.Clamp((Power * PowerInput) + PowerSpike, 0.0, 1.0);
            }
            else
            {
                PowerOutput = 0.0;
            }

            await ControlDevice();
            await Task.Delay(100);
        }
        
    }
    
    public void Initialize(Service_Logs logService)
    {
        if (EnableIntiface)
        {
            return;
        }
        try 
        {
            LogService.AddLog(ServiceName, "System", "Starting IntifaceRunner", Severity.Normal, Variant.Outlined);
            Task.Run( async () => await IntifaceRunner(logService, CancellationToken.None));
            EnableIntiface = true;
        }
        catch (Exception e) 
        {
            LogService.AddLog(ServiceName, "System", "IntifaceRunner failed: " + e.Message, Severity.Error, Variant.Outlined); 
            EnableIntiface = false;
        }
    }
    
    private bool TriggerWithinTolerance(double value, double tolerance = 0.03)
    {
        return value > tolerance || value < -tolerance;
    }
    
    public decimal CreateSineWave( double freq, double amplitude, double offset)
    {
        DateTime currentTime = DateTime.Now;
        var time = currentTime.TimeOfDay.TotalSeconds; // Current time in seconds
        return (decimal)((amplitude * Math.Sin(2 * Math.PI * freq * time)) + offset);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _client.Dispose();
        _client.DeviceAdded -= HandleDeviceAdded;
        _client.DeviceRemoved -= HandleDeviceRemoved;
    }
}