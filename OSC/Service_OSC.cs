﻿using System.Text.Json;
using System.Text.Json.Nodes;
using CoreOSC;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using MudBlazor;
using ZeniControlSuite.Models;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.OSC;

public class Service_OSC : IHostedService
{
    private readonly Service_Logs LogService;
    private readonly Service_AvatarControls AvatarsService;
    public Service_OSC(Service_Logs serviceLogs, Service_AvatarControls serviceAvatars) { LogService = serviceLogs; AvatarsService = serviceAvatars; }

    private void Log(string message, Severity severity = Severity.Normal)
    {
        LogService.AddLog("Service_OSC", "System", message, severity, Variant.Outlined);
    }

    //===========================================//
    #region HostedService Stuff 
    //public delegate void RequestOSCUpdate();
    //public event RequestOSCUpdate? OnOSCUpdate;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task.Run(() => RunOSC(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (Running)
        {
            Running = false;
        }
        return Task.CompletedTask;
    }
    /*    
    public void InvokeOSCUpdate()
    {
        if (OnOSCUpdate != null)
        {
            OnOSCUpdate.Invoke();
        }
    }
    */
    #endregion


    //===========================================//
    #region Settings
    public delegate void OscSubscriptionEventHandler(OSCSubscriptionEvent e);
    public event OscSubscriptionEventHandler? OnOscMessageReceived;

    private UDPListener _listener;
    private UDPSender _sender;
    private CancellationTokenSource _cts;

    private string IP = "127.0.0.1";
    private int listeningPort = 9001;
    private int sendingPort = 9000;
    private bool OSCQuery = false;

    public bool Running { get; private set; } = false;

    #endregion

    #region Config Reading

    public Task ValidateOSCConfig()
    {
        if (!File.Exists("Configs/OSC.json"))
        {
            Log("OSC Config Not Found, creating...", Severity.Warning);
            CreateDefaultConfig();
        }
        var json = File.ReadAllText("Configs/OSC.json");
        var config = JsonSerializer.Deserialize<JsonElement>(json);

        try
        {
            IP = config.GetProperty("IP").GetString() ?? "127.0.0.1";
            listeningPort = config.GetProperty("ListeningPort").GetInt32();
            sendingPort = config.GetProperty("SendingPort").GetInt32();
            OSCQuery = config.GetProperty("OSCQuery").GetBoolean();
        }
        catch (Exception ex)
        {
            Log($"Error loading OSC Config: {ex.Message}", Severity.Error);
        }

        //Log($"OSC Config Loaded: {addressString} Listening on {listeningPort} & sending to {sendingPort}", Severity.Info);
        return Task.CompletedTask;
    }
    public void CreateDefaultConfig()
    {
        //create a json file from the default config
        var defaultConfig = new {
            IP = "127.0.0.1",
            ListingPort = 9001,
            SendingPort = 9000,
            OSCQuery = false
        };
        var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("Configs/OSC.json", json);
    }
    #endregion


    //===========================================//
    #region OSC Logs
    public List<OscMessage> OscLogs { get; private set; } = new();
    private void LogOSC(OscMessage message)
    {
        OscLogs.Add(message);
        if (OscLogs.Count > 100)
        {
            OscLogs.RemoveAt(0);
        }
    }

    #endregion

    //===========================================//
    #region Running Service
    private async Task RunOSC(CancellationToken stoppingToken)
    {
        await ValidateOSCConfig();

        Running = true;
        HandleOscPacket callback = delegate (OscPacket packet)
        {
            var messageReceived = (OscMessage)packet;
            if (messageReceived != null)
            {
                LogOSC(messageReceived);
                Console.WriteLine($"OSC Message Received: {messageReceived.Address}/{messageReceived.Arguments[0]}");

                if (AvatarsService.selectedAvatar.Parameters.ContainsKey(messageReceived.Address))
                {
                    var parameter = AvatarsService.selectedAvatar.Parameters[messageReceived.Address];
                    parameter.Value = FormatIncoming(messageReceived.Arguments[0], parameter.Type);
                    AvatarsService.SetParameterValue(parameter);
                    AvatarsService.InvokeAvatarControlsUpdate();
                }
            }
        };

        try
        {
            Log($"Listening on {listeningPort} & Sending on {sendingPort}");
            Console.WriteLine("");

            _sender = new UDPSender(IP, sendingPort);
            _listener = new UDPListener(listeningPort, callback);
            await Task.Delay(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Log($"Error starting OSC: {ex.Message}", Severity.Error);
            _cts.Cancel();
        }

        Running = false;
    }

    public void StopService()
    {
        if (Running)
        {
            Running = false;
            _sender.Close();
            _listener.Close();
            Log("OSC Service Stopped", Severity.Info);
        }
    }

    public void StartService()
    {
        if (!Running)
        {
            StartAsync(CancellationToken.None);
        }
    }

    #endregion


    //===========================================//
    #region OSC Stuff
    public void sendOSCParameter(Parameter param)
    {
        var value = FormatOutGoing(param.Value, param.Type);

        if (value == null)
        {
            Log($"Error formatting OSC message: {param.Address}", Severity.Error);
            return;
        }

        var message = new OscMessage(param.Address, value);
        try
        {
            _sender.Send(message);
        }
        catch (Exception ex)
        {
            Log($"Error sending OSC message: {ex.Message}", Severity.Error);
        }
    }

    #endregion


    //===========================================//
    #region Helper Functions
    public object? FormatOutGoing(float value, ParameterType type)
    {
        switch (type)
        {
            case ParameterType.Bool:
                return value < 0.5 ? false : true;
            case ParameterType.Int:
                return (int)value;
            case ParameterType.Float:
                return (float)value;
            default:
                return null;
        }
    }

    public float FormatIncoming(object value, ParameterType type)
    {
        switch (type)
        {
            case ParameterType.Bool:
                return (bool)value ? 1 : 0;
            case ParameterType.Int:
                return (int)value;
            case ParameterType.Float:
                return (float)value;
            default:
                return 0;
        }
    }
    #endregion

}