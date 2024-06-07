using System.Timers;
using MudBlazor;
using Buttplug.Client;
using Buttplug.Client.Connectors.WebsocketConnector;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components;

public class Service_Intiface : IHostedService
{
    public delegate void IntifaceUpdate();
    public event IntifaceUpdate? OnIntifaceUpdate;
    public string pageName = "Service_Intiface";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public  void Update()
    {
        if (OnIntifaceUpdate != null)
            OnIntifaceUpdate();
    }

    public void EnableIntiface(Service_Logs LogService)
    {
        if (enabled)
        {
            return;
        }
        try 
        {
            LogService.AddLog(pageName, "System", "Starting IntifaceRunner", Severity.Normal, Variant.Outlined);
            IntifaceRunner(LogService, CancellationToken.None);
            enabled = true;
        }
        catch (Exception e) 
        {
            LogService.AddLog(pageName, "System", "IntifaceRunner failed: " + e.Message, Severity.Error, Variant.Outlined); 
            enabled = false;
        }
    }


    public bool enabled { get; set; } = false;
    public bool intifaceConnected { get; set; } = false;
    public bool deviceConnected { get; set; } = false;
    public string serverAddress { get; set; } = "";

    public double powerInput = 1.0;
    public double power = 0.0;
    public double powerSpike = 0.0;
    public double powerOutput = 0.0;
    public double powerOutputDisplay { get; set; } = 0.0;
    public bool powerFullStop { get; set; } = false;

    //list of pattern types
    public List<string> powerPatterns = new List<string> { 
        "Constant",
        "Wave",
    };

    //pattern settings
    public string patType = "Wave";
    public double patSpeedClimb = 2.0;
    public double patSpeedDrop = 2.0;
    public bool patRunning { get; set; } = false;

    public double patRandomOffTimeMin = 0.5; //time in seconds to wait before turning on
    public double patRandomOffTimeMax = 2.0;
    public double patRandomOnTimeMin = 0.5; //time in seconds to wait before turning off
    public double patRandomOnTimeMax = 2.0;


    public bool patUseRandomPower { get; set; } = false;
    public int patState = 0;
    public double patPowerGoal = 0.0;
    public double patRandomPowerMin = 0.1;
    public double patRandomPowerMax = 1.0;

    private async Task delayRandom(double min, double max)
    {
        double delay = new Random().NextDouble() * (max - min) + min;
        await Task.Delay((int)(delay * 1000));
    }

    private async Task RunPattern()
    {
        patState = 0;
        patPowerGoal = patRandomPowerMax;

        if (patUseRandomPower)
        {
            patPowerGoal = new Random().NextDouble() * (patRandomPowerMax - patRandomPowerMin) + patRandomPowerMin;
        }

        power = 0.0;
        switch (patType)
        {
            case "Constant":
                power = patPowerGoal;
                await delayRandom(patRandomOnTimeMin, patRandomOnTimeMax);
                break;

            case "Wave":
                //states= 0: Up, 1: Down
                while (patState == 0)
                {
                    power += (0.01 * patSpeedClimb) * patPowerGoal;

                    if (power > 0.99 * patPowerGoal)
                    {
                        patState = 1;
                        power = patPowerGoal;
                    }
                    await Task.Delay(50);
                }
                await delayRandom(patRandomOnTimeMin, patRandomOnTimeMax);
                while (patState == 1)
                {
                    power -= (0.01 * patSpeedDrop) * patPowerGoal;

                    if (power < 0.01 + patRandomPowerMin)
                    {
                        patState = 2;
                        power = 0.0 + patRandomPowerMin;
                    }
                    await Task.Delay(50);
                }
                break;
        }

        //Random off Time
        await delayRandom(patRandomOffTimeMin, patRandomOffTimeMax);

        patRunning = false;
    }

    public decimal CreateSineWave( double freq, double amplitude, double offset)
    {
        DateTime currentTime = DateTime.Now;
        var time = currentTime.TimeOfDay.TotalSeconds; // Current time in seconds
        return (decimal)((amplitude * Math.Sin(2 * Math.PI * freq * time)) + offset);
    }

   
    
    
    
    public async Task IntifaceRunner(Service_Logs LogService, CancellationToken cancellationToken)
    {
        #region Startup
        var client = new ButtplugClient($"ZeniControlSuite");

        void HandleDeviceAdded(object aObj, DeviceAddedEventArgs aArgs)
        {
            LogService.AddLog(pageName, "System", $"Device Added: {aArgs.Device.Name}", Severity.Normal, Variant.Outlined);
            deviceConnected = true;
        }

        client.DeviceAdded += HandleDeviceAdded;

        void HandleDeviceRemoved(object aObj, DeviceRemovedEventArgs aArgs)
        {
            LogService.AddLog(pageName, "System", $"Device Removed: {aArgs.Device.Name}", Severity.Normal, Variant.Outlined);
            deviceConnected = false;
        }

        client.DeviceRemoved += HandleDeviceRemoved;

        try
        {
            await client.ConnectAsync(new ButtplugWebsocketConnector(new Uri("ws://localhost:16261")));
            LogService.AddLog(pageName, "System", "Connected to Intiface client", Severity.Normal, Variant.Outlined);
            intifaceConnected = true;
        }
        catch (Exception e)
        {
            LogService.AddLog(pageName, "System", $"Error connecting to Intiface client: {e.Message}", Severity.Error, Variant.Outlined);
            intifaceConnected = false;
            enabled = false;
            return;
        }

        async Task ScanForDevices()
        {
            await client.StartScanningAsync();

            while (deviceConnected == false)
            {
                await Task.Delay(50);
            }

            await client.StopScanningAsync();
        }

        await ScanForDevices();
        #endregion

        async Task ControlDevice()
        {
            foreach (var device in client.Devices)
            {
                await device.VibrateAsync(powerOutput);
                powerOutputDisplay = powerOutput * 100;
            }
        }

        Task.Run( async () =>
        {
            while (enabled)
            {
                if (!patRunning)
                {
                    patRunning = true;
                    RunPattern().GetAwaiter();
                }

                if (!powerFullStop)
                {
                    powerOutput = Math.Clamp((power * powerInput) + powerSpike, 0.0, 1.0);
                }
                else
                {
                    powerOutput = 0.0;
                }

                await ControlDevice();
                await Task.Delay(50);
                if(TriggerWithinTolerance(powerOutput))
                {
                    Update();
                }
            }
        });


    }

    private async void UpdatePower()
    {
        
    }
    
    private bool TriggerWithinTolerance(double value, double tolerance = 1.0)
    {
        return value > 0.5 - tolerance && value < 0.5 + tolerance;
    }
}
