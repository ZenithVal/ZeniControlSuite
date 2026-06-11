using System.Net;
using System.Text.Json;
using FastOSC;
using MudBlazor;
using VRC.OSCQuery;
using ZeniControlSuite.Extensions;
using ZeniControlSuite.Models;
using SuiteOscMessage = ZeniControlSuite.Models.OscMessage;
using FastOscMessage = FastOSC.OSCMessage;

namespace ZeniControlSuite.Services;

public class Service_OSC : IHostedService, IDisposable
{
    public delegate void OscSubscriptionEventHandler(SuiteOscMessage e);
    public event OscSubscriptionEventHandler? OnOscMessageReceived;

    private const string OscQueryServiceName = "Zeni Control Suite";

    private readonly Service_Logs LogService;
    private readonly Service_AccessCodes AccessCodes;
    private readonly object _logLock = new();
    private readonly object _clientLock = new();
    private readonly object _discoveredParametersLock = new();

    private OSCSender? _sender;
    private OSCReceiver? _receiver;
    private OSCQueryService? _oscQueryService;
    private bool _senderConnected;
    private bool _receiverConnected;

    private string IP = "127.0.0.1";
    public int listeningPort = 9001;
    public int sendingPort = 9000;
    private bool useOSCQuery;
    private bool paramLogging;

    private List<DiscoveredOscParameter> _discoveredAvatarParameters = new();

    public Service_OSC(Service_Logs serviceLogs, Service_AccessCodes accessCodes)
    {
        LogService = serviceLogs;
        AccessCodes = accessCodes;
    }

    public bool Running { get; private set; }
    public bool OscQueryRunning => _oscQueryService != null;
    public DateTimeOffset LastAvatarParameterDiscovery { get; private set; } = DateTimeOffset.MinValue;

    public IReadOnlyList<DiscoveredOscParameter> DiscoveredAvatarParameters
    {
        get
        {
            lock (_discoveredParametersLock)
            {
                return _discoveredAvatarParameters.OrderBy(parameter => parameter.DisplayName).ToList();
            }
        }
    }

    public List<SuiteOscMessage> OscLogs { get; private set; } = new();

    public Task StartAsync(CancellationToken cancellationToken) => RunOSC(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => StopServiceAsync(cancellationToken);

    private void Log(string message, Severity severity = Severity.Normal)
    {
        LogService.AddLog("Service_OSC", "System", message, severity, Variant.Outlined);
    }

    public async Task InitializeOSCConfig()
    {
        if (!File.Exists("Configs/OSC.json"))
        {
            Log("OSC config not found; creating default config.", Severity.Warning);
            CreateDefaultConfig();
        }

        try
        {
            var json = await File.ReadAllTextAsync("Configs/OSC.json");
            var config = JsonSerializer.Deserialize<OscConfig>(json) ?? new OscConfig();

            IP = string.IsNullOrWhiteSpace(config.IP) ? "127.0.0.1" : config.IP;
            sendingPort = config.SendingPort > 0 ? config.SendingPort : 9000;
            listeningPort = config.ListeningPort > 0 ? config.ListeningPort : 9001;
            useOSCQuery = config.UseOSCQuery;
            paramLogging = config.ParamLogging;
        }
        catch (Exception ex)
        {
            Log($"Error loading OSC config: {ex.Message}", Severity.Error);
        }
    }

    public void CreateDefaultConfig()
    {
        Directory.CreateDirectory("Configs");
        var defaultConfig = new OscConfig();
        var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("Configs/OSC.json", json);
    }

    private async Task RunOSC(CancellationToken stoppingToken)
    {
        if (Running)
        {
            return;
        }

        await InitializeOSCConfig();
        await DisconnectFastOscAsync();
        StopOscQuery();

        try
        {
            var configuredListeningPort = listeningPort;
            var activeListeningPort = configuredListeningPort;

            if (useOSCQuery)
            {
                activeListeningPort = VRC.OSCQuery.Extensions.GetAvailableUdpPort();
            }

            listeningPort = activeListeningPort;

            if (!IPAddress.TryParse(IP, out var sendIp))
            {
                sendIp = IPAddress.Loopback;
                Log($"Invalid OSC IP '{IP}', falling back to 127.0.0.1.", Severity.Warning);
            }

            await InitializeSenderAsync(sendIp, sendingPort, stoppingToken);
            InitializeReceiver(activeListeningPort);

            if (useOSCQuery)
            {
                StartOscQuery(activeListeningPort);
            }

            Running = true;
            Log($"OSC started. Listening on UDP {activeListeningPort}; sending to {IP}:{sendingPort}.", Severity.Info);
        }
        catch (OperationCanceledException)
        {
            await DisconnectFastOscAsync();
            StopOscQuery();
            Running = false;
        }
        catch (Exception ex)
        {
            await DisconnectFastOscAsync();
            StopOscQuery();
            Running = false;
            Log($"Error starting OSC: {ex.Message}", Severity.Error);
        }
    }

    private async Task InitializeSenderAsync(IPAddress sendIp, int port, CancellationToken cancellationToken)
    {
        var endpoint = new IPEndPoint(sendIp, port);
        var sender = new OSCSender();
        await sender.ConnectAsync(endpoint);

        lock (_clientLock)
        {
            _sender = sender;
            _senderConnected = true;
        }

        Log($"OSC sender connected to {endpoint.Address}:{endpoint.Port}.", Severity.Info);
        SendVisitorCodeAfterSenderConnection(sender);
    }

    private void InitializeReceiver(int port)
    {
        var receiver = new OSCReceiver();
        receiver.OnPacketReceived += HandleFastOscPacketAsync;
        receiver.Connect(new IPEndPoint(IPAddress.Any, port));

        lock (_clientLock)
        {
            _receiver = receiver;
            _receiverConnected = true;
        }
    }

    private void StartOscQuery(int udpPort)
    {
        try
        {
            var tcpPort = VRC.OSCQuery.Extensions.GetAvailableTcpPort();

            _oscQueryService = new OSCQueryServiceBuilder()
                .WithServiceName(OscQueryServiceName)
                .WithUdpPort(udpPort)
                .WithTcpPort(tcpPort)
                .WithDefaults()
                .Build();

            AdvertiseEndpoints();
            Log($"OSCQuery advertising on UDP {udpPort}, TCP {tcpPort}.", Severity.Info);
        }
        catch (Exception ex)
        {
            _oscQueryService = null;
            Log($"OSCQuery startup failed: {ex.Message}", Severity.Error);
        }
    }

    private void AdvertiseEndpoints()
    {
        if (_oscQueryService == null)
        {
            return;
        }

        TryAddOscQueryEndpoint("/avatar/change", "s", VRC.OSCQuery.Attributes.AccessValues.WriteOnly, "VRChat avatar change events");
        TryAddOscQueryEndpoint(AccessCodes.VisitorCodeOscAddress, "i", VRC.OSCQuery.Attributes.AccessValues.WriteOnly, "Visitor code");
    }

    private void TryAddOscQueryEndpoint(string path, string typeTag, VRC.OSCQuery.Attributes.AccessValues access, string description)
    {
        if (_oscQueryService == null)
        {
            return;
        }

        try
        {
            _oscQueryService.AddEndpoint(path, typeTag, access, null, description);
        }
        catch (Exception ex)
        {
            Log($"OSCQuery endpoint registration failed for {path}: {ex.Message}", Severity.Warning);
        }
    }

    private void SendVisitorCodeAfterSenderConnection(OSCSender sender)
    {
        try
        {
            var value = OSCExtensions.FormatOutGoing(AccessCodes.VisitorCodeParameter.Value, AccessCodes.VisitorCodeParameter.Type);
            if (value == null)
            {
                return;
            }

            sender.Send(new FastOscMessage(AccessCodes.VisitorCodeParameter.Address, NormalizeArguments(new object[] { value }).ToArray()));
        }
        catch (Exception ex)
        {
            Log($"Visitor code send after OSC sender connection failed: {ex.Message}", Severity.Warning);
        }
    }

    private Task HandleFastOscPacketAsync(IOSCPacket packet)
    {
        try
        {
            DispatchFastOscPacket(packet);
        }
        catch (Exception ex)
        {
            Log($"OSC packet handler error: {ex.Message}", Severity.Error);
        }

        return Task.CompletedTask;
    }

    private void DispatchFastOscPacket(IOSCPacket packet)
    {
        switch (packet)
        {
            case FastOscMessage message:
                var suiteMessage = new SuiteOscMessage(message.Address, message.Arguments);
                if (paramLogging)
                {
                    LogOSC(suiteMessage);
                }
                RememberDiscoveredAvatarParameter(message.Address, suiteMessage.FirstOrDefault(), "Incoming OSC");
                DispatchOscMessage(suiteMessage);
                break;

            case OSCBundle bundle:
                foreach (var child in bundle.Packets)
                {
                    DispatchFastOscPacket(child);
                }
                break;
        }
    }

    private void DispatchOscMessage(SuiteOscMessage message)
    {
        var handlers = OnOscMessageReceived;
        if (handlers == null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<OscSubscriptionEventHandler>())
        {
            try
            {
                handler(message);
            }
            catch (Exception ex)
            {
                Log($"OSC subscriber error for {message.Address}: {ex.Message}", Severity.Error);
            }
        }
    }

    private void LogOSC(SuiteOscMessage message)
    {
        lock (_logLock)
        {
            OscLogs.Add(message);
            if (OscLogs.Count > 500)
            {
                OscLogs.RemoveAt(0);
            }
        }
    }

    public void RefreshOscQuery()
    {
        if (!useOSCQuery || !Running)
        {
            return;
        }

        try
        {
            _oscQueryService?.RefreshServices();
            Log("OSCQuery service discovery refreshed.", Severity.Info);
        }
        catch (Exception ex)
        {
            Log($"OSCQuery refresh failed: {ex.Message}", Severity.Warning);
        }
    }

    public void RegisterOscQueryEndpoint(string path, string typeTag, string description)
    {
        TryAddOscQueryEndpoint(path, typeTag, VRC.OSCQuery.Attributes.AccessValues.ReadWrite, description);
    }

    public Task<IReadOnlyList<DiscoveredOscParameter>> RefreshAvatarParametersFromOscQueryAsync(CancellationToken cancellationToken = default)
    {
        LastAvatarParameterDiscovery = DateTimeOffset.UtcNow;
        Log("Avatar parameter editing now loads VRChat avatar JSON files. OSCQuery parameter refresh was skipped.", Severity.Info);
        return Task.FromResult(DiscoveredAvatarParameters);
    }

    public void ReplaceDiscoveredAvatarParameters(IEnumerable<DiscoveredOscParameter> parameters, string source)
    {
        lock (_discoveredParametersLock)
        {
            _discoveredAvatarParameters = parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Address))
                .Select(parameter =>
                {
                    parameter.Source = string.IsNullOrWhiteSpace(parameter.Source) ? source : parameter.Source;
                    parameter.LastSeen = DateTimeOffset.UtcNow;
                    return parameter;
                })
                .GroupBy(parameter => parameter.Address, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(parameter => parameter.DisplayName)
                .ToList();

            LastAvatarParameterDiscovery = DateTimeOffset.UtcNow;
        }

        Log($"Loaded {_discoveredAvatarParameters.Count} avatar parameter(s) from {source}.", Severity.Info);
    }

    public void ClearDiscoveredAvatarParameters(string reason = "avatar switch")
    {
        lock (_discoveredParametersLock)
        {
            if (_discoveredAvatarParameters.Count == 0)
            {
                return;
            }

            _discoveredAvatarParameters.Clear();
            LastAvatarParameterDiscovery = DateTimeOffset.MinValue;
        }

        Log($"Cleared loaded avatar parameters after {reason}.", Severity.Info);
    }

    private void RememberDiscoveredAvatarParameter(string address, object? value, string source)
    {
        if (!address.StartsWith("/avatar/parameters/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        MergeDiscoveredAvatarParameters(new[]
        {
            new DiscoveredOscParameter
            {
                Address = address,
                Type = InferParameterTypeFromValue(value),
                Source = source,
                LastSeen = DateTimeOffset.UtcNow
            }
        });
    }

    private void MergeDiscoveredAvatarParameters(IEnumerable<DiscoveredOscParameter> parameters)
    {
        lock (_discoveredParametersLock)
        {
            var byAddress = _discoveredAvatarParameters.ToDictionary(parameter => parameter.Address, StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in parameters.Where(parameter => !string.IsNullOrWhiteSpace(parameter.Address)))
            {
                byAddress[parameter.Address] = parameter;
            }

            _discoveredAvatarParameters = byAddress.Values.OrderBy(parameter => parameter.DisplayName).ToList();
        }
    }

    private static ParameterType InferParameterTypeFromValue(object? value)
    {
        return value switch
        {
            bool => ParameterType.Bool,
            int => ParameterType.Int,
            long => ParameterType.Int,
            short => ParameterType.Int,
            byte => ParameterType.Int,
            float => ParameterType.Float,
            double => ParameterType.Float,
            decimal => ParameterType.Float,
            _ => ParameterType.Bool
        };
    }

    public void StopService()
    {
        _ = StopServiceAsync(CancellationToken.None);
    }

    public void StartService()
    {
        if (!Running)
        {
            _ = StartAsync(CancellationToken.None);
        }
    }

    private async Task StopServiceAsync(CancellationToken cancellationToken)
    {
        StopOscQuery();
        await DisconnectFastOscAsync();
        Running = false;
        Log("OSC service stopped", Severity.Info);
    }

    public void sendOSCParameter(Parameter param)
    {
        var value = OSCExtensions.FormatOutGoing(param.Value, param.Type);
        if (value == null)
        {
            Log($"Error formatting OSC message: {param.Address}", Severity.Error);
            return;
        }

        sendOSCMessage(param.Address, value);
    }

    public void sendOSCMessage(string address, object value)
    {
        Send(new SuiteOscMessage(address, value));
    }

    public void Send(SuiteOscMessage message)
    {
        _ = SendAsync(message);
    }

    public Task SendAsync(SuiteOscMessage message)
    {
        OSCSender? sender;
        bool senderConnected;

        lock (_clientLock)
        {
            sender = _sender;
            senderConnected = _senderConnected;
        }

        if (sender == null || !senderConnected)
        {
            Log($"OSC send skipped; service is not ready: {message.Address}", Severity.Warning);
            return Task.CompletedTask;
        }

        try
        {
            var arguments = NormalizeArguments(message.Arguments).ToArray();
            if (arguments.Length == 0)
            {
                Log($"OSC send skipped; FastOSC messages require at least one argument: {message.Address}", Severity.Warning);
                return Task.CompletedTask;
            }

            sender.Send(new FastOscMessage(message.Address, arguments));
        }
        catch (ObjectDisposedException)
        {
            Log($"OSC send skipped; sender is closed: {message.Address}", Severity.Warning);
        }
        catch (InvalidOperationException ex)
        {
            Log($"OSC send skipped; sender is disconnected: {message.Address} ({ex.Message})", Severity.Warning);
        }
        catch (Exception ex)
        {
            Log($"Error sending OSC message {message.Address}: {ex.Message}", Severity.Error);
        }

        return Task.CompletedTask;
    }

    private static IEnumerable<object> NormalizeArguments(IEnumerable<object> arguments)
    {
        foreach (var argument in arguments)
        {
            yield return argument switch
            {
                double value => (float)value,
                decimal value => (float)value,
                long value => unchecked((int)value),
                short value => (int)value,
                byte value => (int)value,
                null => string.Empty,
                _ => argument
            };
        }
    }

    private async Task DisconnectFastOscAsync()
    {
        OSCSender? sender;
        OSCReceiver? receiver;
        bool senderConnected;
        bool receiverConnected;

        lock (_clientLock)
        {
            sender = _sender;
            receiver = _receiver;
            senderConnected = _senderConnected;
            receiverConnected = _receiverConnected;
            _sender = null;
            _receiver = null;
            _senderConnected = false;
            _receiverConnected = false;
        }

        if (receiver != null && receiverConnected)
        {
            try
            {
                receiver.OnPacketReceived -= HandleFastOscPacketAsync;
                await receiver.DisconnectAsync();
            }
            catch (ObjectDisposedException) { }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException) { }
            catch (Exception ex)
            {
                Log($"OSC receiver shutdown failed: {ex.Message}", Severity.Warning);
            }
        }

        if (sender != null && senderConnected)
        {
            try
            {
                sender.Disconnect();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
            catch (Exception ex)
            {
                Log($"OSC sender shutdown failed: {ex.Message}", Severity.Warning);
            }
        }
    }

    private void StopOscQuery()
    {
        try
        {
            _oscQueryService?.Dispose();
        }
        catch (Exception ex)
        {
            Log($"OSCQuery shutdown failed: {ex.Message}", Severity.Warning);
        }
        finally
        {
            _oscQueryService = null;
        }
    }

    public void Dispose()
    {
        StopOscQuery();
        try
        {
            DisconnectFastOscAsync().GetAwaiter().GetResult();
        }
        catch
        {
        }
    }

    private sealed class OscConfig
    {
        public string IP { get; set; } = "127.0.0.1";
        public int SendingPort { get; set; } = 9000;
        public int ListeningPort { get; set; } = 9001;
        public bool UseOSCQuery { get; set; } = true;
        public bool ParamLogging { get; set; } = false;
    }
}
