using MudBlazor;
namespace ZeniControlSuite.Models;

public class LogEvent
{
    public string source { get; set; } = "Undefined!";
    public string user { get; set; } = "Undefined!";
    public DateTime time { get; set; } = DateTime.Now;
    public string message { get; set; } = "Undefined!";
    public Severity severity { get; set; } = Severity.Normal;
    public Variant variant { get; set; } = Variant.Outlined;
}


