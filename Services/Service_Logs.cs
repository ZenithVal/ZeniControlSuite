using MudBlazor;

namespace ZeniControlSuite.Services;
public class Service_Logs : IHostedService
{
    //===========================================//
    #region HostedService Stuff 
    public delegate void RequestLogsUpdate();
    public event RequestLogsUpdate? OnLogsUpdate;
    public Task StartAsync(CancellationToken cancellationToken)
    {
        AddLog("Service_Logs", "System", "Service Started", Severity.Normal, Variant.Outlined);
        Console.WriteLine("");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    #endregion


    //===========================================//
    #region Log Stuff
    public List<LogEvent> logEvents { get; private set; } = new();

    public void AddLog(string source, string user, string message, Severity severity = Severity.Normal, Variant variant = Variant.Outlined)
    {
        logEvents.Add(new LogEvent { source = source, user = user, message = message, severity = severity, variant = variant });
        Console.WriteLine($"{severity} | {user} | {source}: {message}");
        //Console.WriteLine(user == "System" ? $"{severity} | {source}: {message}" : $"{severity} | {user} | {source}: {message}");
        InvokeLogsIpdate();

        if (logEvents.Count > 100)
        {
            logEvents.RemoveAt(0);
        }

    }

    public void InvokeLogsIpdate()
    {
        OnLogsUpdate?.Invoke();
    }
    #endregion
}

