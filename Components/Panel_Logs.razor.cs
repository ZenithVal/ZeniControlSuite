using Microsoft.AspNetCore.Components;
namespace ZeniControlSuite.Components;

public partial class Panel_Logs : IDisposable
{
    [Inject] private LogService LogService { get; set; } = default!;

    protected override void OnInitialized()
    {
        LogService.OnLogsUpdate += OnLogsUpdate;
    }
    private void OnLogsUpdate()
    {
        InvokeAsync(StateHasChanged);
    }
    public void Dispose()
    {
        LogService.OnLogsUpdate -= OnLogsUpdate;
    }
}