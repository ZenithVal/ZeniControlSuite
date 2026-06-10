using System.Net;
using System.Net.Http;
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

    private readonly Service_Logs LogService;
    private readonly Service_AccessCodes AccessCodes;

    public Service_OSC(Service_Logs serviceLogs, Service_AccessCodes accessCodes)
    {
        LogService = serviceLogs;
        AccessCodes = accessCodes;
    }

    private void Log(string message, Severity severity = Severity.Normal)
    {
        LogService.AddLog("Service_OSC", "System", message, severity, Variant.Outlined);
    }

    private readonly object _logLock = new();
    private readonly object _clientLock = new();
    private readonly object _discoveredParametersLock = new();
    private OSCSender? _sender;
    private OSCReceiver? _receiver;
    private IPEndPoint? _sendEndPoint;
    private IPEndPoint? _listenEndPoint;
    private OSCQueryService? _oscQueryService;
    private bool _senderConnected;
    private bool _receiverConnected;
    private readonly SemaphoreSlim _senderReconnectLock = new(1, 1);
    private const string VrchatOscServicePrefix = "VRChat-Client";

    private string IP = "127.0.0.1";
    public int listeningPort = 9001;
	public int sendingPort = 9000;
    private bool useOSCQuery = false;
    private bool paramLogging = false;
    private const string OscQueryServiceName = "Zeni Control Suite";
    private readonly object _oscQueryTargetsLock = new();
    private List<OscQueryTarget> oscQueryTargets = new();
    private readonly HashSet<string> _loggedOscServices = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loggedOscQueryServices = new(StringComparer.OrdinalIgnoreCase);
    private List<DiscoveredOscParameter> _discoveredAvatarParameters = new();

    public bool Running { get; private set; } = false;
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

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RunOSC(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopServiceAsync(cancellationToken);
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
            listeningPort = config.ListeningPort > 0 ? config.ListeningPort : 9001;
            sendingPort = config.SendingPort > 0 ? config.SendingPort : 9000;
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

    public List<SuiteOscMessage> OscLogs { get; private set; } = new();

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

    private async Task RunOSC(CancellationToken stoppingToken)
    {
        if (Running)
        {
            return;
        }

        await InitializeOSCConfig();

        try
        {
            if (!IPAddress.TryParse(IP, out var sendIp))
            {
                sendIp = IPAddress.Loopback;
                Log($"Invalid OSC IP '{IP}', falling back to 127.0.0.1.", Severity.Warning);
            }

			if (useOSCQuery)
            {
				int udpPort = VRC.OSCQuery.Extensions.GetAvailableUdpPort();
				int tcpPort = VRC.OSCQuery.Extensions.GetAvailableTcpPort();

                listeningPort = udpPort;
			}

            var receiver = new OSCReceiver(2048);
            var listenEndpoint = new IPEndPoint(IPAddress.Any, listeningPort);
            receiver.OnPacketReceived += HandleFastOscPacketAsync;
            receiver.Connect(listenEndpoint);

            lock (_clientLock)
            {
                _receiver = receiver;
                _listenEndPoint = listenEndpoint;
                _receiverConnected = true;
                _senderConnected = false;
            }

            await ConnectSenderAsync(sendingPort, "configured OSC send port", stoppingToken);

            Running = true;
            Log($"Listening on {listeningPort}; sending to {sendingPort}));", Severity.Info);

			if (useOSCQuery)
            {
                StartOscQuery();
            }
        }
        catch (OperationCanceledException)
        {
            Running = false;
            await DisconnectFastOscAsync();
            StopOscQuery();
        }
        catch (Exception ex)
        {
            Running = false;
            await DisconnectFastOscAsync();
            StopOscQuery();
            Log($"Error starting OSC: {ex.Message}", Severity.Error);
        }
    }

    private async Task ConnectSenderAsync(int port, string source, CancellationToken cancellationToken = default)
    {
        if (port <= 0)
        {
            Log($"OSC sender target from {source} had an invalid port: {port}", Severity.Warning);
            return;
        }

        await _senderReconnectLock.WaitAsync(cancellationToken);
        try
        {
            if (!IPAddress.TryParse(IP, out var sendIp))
            {
                sendIp = IPAddress.Loopback;
            }

            var endpoint = new IPEndPoint(sendIp, port);
            var sender = new OSCSender();
            await sender.ConnectAsync(endpoint);

            OSCSender? oldSender;
            lock (_clientLock)
            {
                oldSender = _sender;
                _sender = sender;
                _sendEndPoint = endpoint;
                sendingPort = port;
                _senderConnected = true;
            }

            try
            {
                oldSender?.Disconnect();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }

            Log($"OSC sender connected to {IP}:{port} ({source}).", Severity.Info);
            SendVisitorCodeAfterSenderConnection(sender);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            lock (_clientLock)
            {
                _senderConnected = false;
            }

            Log($"OSC sender connection failed for {IP}:{port} ({source}): {ex.Message}", Severity.Error);
        }
        finally
        {
            _senderReconnectLock.Release();
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
            //Log($"Sent visitor code to avatar after OSC sender connection: {AccessCodes.VisitorCodeDisplay}", Severity.Info);
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
                if (paramLogging) LogOSC(suiteMessage);
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

    private void StartOscQuery()
    {
        try
        {
            StopOscQuery();

            _oscQueryService = new OSCQueryServiceBuilder()
                .WithServiceName(OscQueryServiceName)
                .WithUdpPort(listeningPort)
                .WithTcpPort(sendingPort)
                .WithDiscovery(new MeaModDiscovery())
                .StartHttpServer()
                .AdvertiseOSC()
                .AdvertiseOSCQuery()
                .Build();

            _oscQueryService.OnOscServiceAdded += profile =>
            {
                if (!IsVrchatService(profile.name))
                {
                    return;
                }

                var address = profile.address.ToString();
                if (RememberLoggedOscService(address, profile.port, profile.name))
                {
                    Log($"OSCQuery found VRChat OSC service.", Severity.Info);
                }
            };

            _oscQueryService.OnOscQueryServiceAdded += profile =>
            {
                if (!IsVrchatService(profile.name))
                {
                    return;
                }

                var address = profile.address.ToString();
                if (RememberOscQueryTarget(address, profile.port, profile.name))
                {
                    Log($"OSCQuery found VRChat query service.", Severity.Info);
                }
            };

            _oscQueryService.AddEndpoint("/avatar/change", "s", VRC.OSCQuery.Attributes.AccessValues.ReadWrite, null, "VRChat avatar change events");
            _oscQueryService.AddEndpoint("/avatar/parameters", "", VRC.OSCQuery.Attributes.AccessValues.ReadWrite, null, "VRChat avatar parameter updates");
            _oscQueryService.AddEndpoint(AccessCodes.VisitorCodeOscAddress, "i", VRC.OSCQuery.Attributes.AccessValues.WriteOnly, null, "Visitor code");

            _oscQueryService.RefreshServices();

            Log($"OSCQuery advertising on UDP {listeningPort}, TCP {sendingPort}.", Severity.Info);
        }
        catch (Exception ex)
        {
            _oscQueryService = null;
            Log($"OSCQuery startup failed: {ex.Message}", Severity.Error);
        }
    }

    private static bool IsVrchatService(string? serviceName)
    {
        return !string.IsNullOrWhiteSpace(serviceName)
            && serviceName.StartsWith(VrchatOscServicePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private bool RememberLoggedOscService(string address, int port, string serviceName)
    {
        if (port <= 0)
        {
            return false;
        }

        lock (_oscQueryTargetsLock)
        {
            return _loggedOscServices.Add($"{address}:{port}:{serviceName}");
        }
    }

    private bool RememberOscQueryTarget(string address, int port, string serviceName)
    {
        if (port <= 0)
        {
            return false;
        }

        lock (_oscQueryTargetsLock)
        {
            var key = $"{address}:{port}:{serviceName}";
            if (!_loggedOscQueryServices.Add(key))
            {
                return false;
            }

            oscQueryTargets.RemoveAll(target =>
                target.Port == port
                && string.Equals(target.Address, address, StringComparison.OrdinalIgnoreCase));

            oscQueryTargets.Add(new OscQueryTarget(address, port, serviceName));
            return true;
        }
    }

    public async Task<IReadOnlyList<DiscoveredOscParameter>> RefreshAvatarParametersFromOscQueryAsync(CancellationToken cancellationToken = default)
    {
        var discovered = new List<DiscoveredOscParameter>();
        var targets = GetOscQueryTargets();

        foreach (var target in targets)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var rootJson = await http.GetStringAsync($"http://{target.Address}:{target.Port}/", cancellationToken);
                using var rootDocument = JsonDocument.Parse(rootJson);
                CollectAvatarParameters(rootDocument.RootElement, discovered, $"OSCQuery:{target.ServiceName}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log($"OSCQuery avatar parameter read failed on {target.Address}:{target.Port}: {ex.Message}", Severity.Info);
            }
        }

        if (discovered.Count > 0)
        {
            MergeDiscoveredAvatarParameters(discovered);
            LastAvatarParameterDiscovery = DateTimeOffset.UtcNow;
            Log($"OSCQuery discovered {discovered.Count} avatar parameter(s).", Severity.Info);
        }
        else
        {
            LastAvatarParameterDiscovery = DateTimeOffset.UtcNow;
            Log("OSCQuery did not return avatar parameters. Existing incoming-OSC observations are still available.", Severity.Warning);
        }

        return DiscoveredAvatarParameters;
    }

    private List<OscQueryTarget> GetOscQueryTargets()
    {
        var targets = new List<OscQueryTarget>();

        lock (_oscQueryTargetsLock)
        {
            targets.AddRange(oscQueryTargets);
        }


        return targets
            .GroupBy(target => $"{target.Address}:{target.Port}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static void CollectAvatarParameters(JsonElement element, List<DiscoveredOscParameter> output, string source)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var address = TryReadString(element, "FULL_PATH") ?? TryReadString(element, "fullPath") ?? TryReadString(element, "PATH") ?? TryReadString(element, "path");
        if (!string.IsNullOrWhiteSpace(address) && address.StartsWith("/avatar/parameters/", StringComparison.OrdinalIgnoreCase))
        {
            output.Add(new DiscoveredOscParameter
            {
                Address = address,
                Type = InferParameterTypeFromOscQueryType(TryReadString(element, "OSC_TYPE") ?? TryReadString(element, "TYPE") ?? TryReadString(element, "type")),
                Source = source,
                LastSeen = DateTimeOffset.UtcNow
            });
        }

        if (element.TryGetProperty("CONTENTS", out var contents) && contents.ValueKind == JsonValueKind.Object)
        {
            foreach (var child in contents.EnumerateObject())
            {
                CollectAvatarParameters(child.Value, output, source);
            }
        }

        if (element.TryGetProperty("contents", out var lowerContents) && lowerContents.ValueKind == JsonValueKind.Object)
        {
            foreach (var child in lowerContents.EnumerateObject())
            {
                CollectAvatarParameters(child.Value, output, source);
            }
        }
    }

    private static string? TryReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
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

    private static ParameterType InferParameterTypeFromOscQueryType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return ParameterType.Bool;
        }

        var normalized = type.Trim().ToLowerInvariant();
        if (normalized.Contains('i') || normalized.Contains("int"))
        {
            return ParameterType.Int;
        }

        if (normalized.Contains('f') || normalized.Contains("float") || normalized.Contains("double"))
        {
            return ParameterType.Float;
        }

        return ParameterType.Bool;
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
            lock (_oscQueryTargetsLock)
            {
                oscQueryTargets.Clear();
                _loggedOscServices.Clear();
                _loggedOscQueryServices.Clear();
            }
        }
    }

    public void RefreshOscQuery()
    {
        if (_oscQueryService == null)
        {
            if (useOSCQuery && Running)
            {
                StartOscQuery();
            }
            return;
        }

        try
        {
            _oscQueryService.RefreshServices();
            Log("OSCQuery service discovery refreshed.", Severity.Info);
        }
        catch (Exception ex)
        {
            Log($"OSCQuery refresh failed: {ex.Message}", Severity.Error);
        }
    }

    public void RegisterOscQueryEndpoint(string path, string typeTag, string description)
    {
        if (_oscQueryService == null)
        {
            return;
        }

        try
        {
            _oscQueryService.AddEndpoint(path, typeTag, VRC.OSCQuery.Attributes.AccessValues.ReadWrite, null, description);
        }
        catch (Exception ex)
        {
            Log($"OSCQuery endpoint registration failed for {path}: {ex.Message}", Severity.Warning);
        }
    }

    public void StopService()
    {
        _ = StopServiceAsync(CancellationToken.None);
    }

    private async Task StopServiceAsync(CancellationToken cancellationToken)
    {
        StopOscQuery();
        await DisconnectFastOscAsync();
        Running = false;
        Log("OSC service stopped", Severity.Info);
    }

    public void StartService()
    {
        if (!Running)
        {
            _ = StartAsync(CancellationToken.None);
        }
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
        var message = new SuiteOscMessage(address, value);
        Send(message);
    }

    public void Send(SuiteOscMessage message)
    {
        _ = SendAsync(message);
    }

    public Task SendAsync(SuiteOscMessage message)
    {
        OSCSender? sender;
        lock (_clientLock)
        {
            sender = _sender;
        }

        if (sender == null || !_senderConnected)
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
            _sendEndPoint = null;
            _listenEndPoint = null;
            _senderConnected = false;
            _receiverConnected = false;
        }

        if (receiver != null && receiverConnected)
        {
            try
            {
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

    public void Dispose()
    {
        StopOscQuery();
        try
        {
            DisconnectFastOscAsync().GetAwaiter().GetResult();
        }
        catch
        {
            //heck
        }
    }


    private sealed record OscQueryTarget(string Address, int Port, string ServiceName);

    private sealed class OscConfig
    {
        public string IP { get; set; } = "127.0.0.1";
        public int SendingPort { get; set; } = 9000;
        public int ListeningPort { get; set; } = 9001;
        public bool UseOSCQuery { get; set; } = true;
        public bool ParamLogging { get; set; } = false;
	}
}
