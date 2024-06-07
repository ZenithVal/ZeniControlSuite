using Microsoft.AspNetCore.Components;
using MudBlazor;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components;

public partial class Panel_Intiface : IDisposable
{
    public static bool pageEnabled = true;
    private string user = "Undefined";
    private string pageName = "Panel_Intiface";

    [Inject] private Service_Logs LogService { get; set; } = default!;
    [Inject] private IntifaceService IntifaceService { get; set; } = default!;

    protected override void OnInitialized()
    {
        IntifaceService.OnRequestDisplayUpdate += OnIntifaceUpdate;
    }

    private void OnIntifaceUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        IntifaceService.OnRequestDisplayUpdate -= OnIntifaceUpdate;
    }

    public void EnableIntiface()
    {
        IntifaceService.Initialize(LogService);
    }

    public void PowerFullStop()
    {
        IntifaceService.FullStop = !IntifaceService.FullStop;
    }

}