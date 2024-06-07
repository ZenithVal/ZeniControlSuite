using Microsoft.AspNetCore.Components;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components;
public partial class Panel_Logs : IDisposable
{
    [Inject] private Service_Logs LogService { get; set; } = default!;

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