using MudBlazor;

namespace ZeniControlSuite.Components;
public class Service_Logs : IHostedService
{
    public delegate void LogsUpdate();
    public event LogsUpdate? OnLogsUpdate;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        AddLog("System", "Null", "Log Service Started", Severity.Normal, Variant.Outlined);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public List<LogEvent> logEvents { get; private set; } = new();

    public void AddLog(string source, string user, string message, Severity severity = Severity.Normal, Variant variant = Variant.Outlined)
    {
        logEvents.Add(new LogEvent {source = source, user = user, message = message, severity = severity, variant = variant });
        Console.WriteLine($"{severity} | {source}: {message}");
        if (OnLogsUpdate != null)
            OnLogsUpdate();

        if (logEvents.Count > 100)
        {
			logEvents.RemoveAt(0);
		}
        //Dont think this matters, but just in case...
    }
}

