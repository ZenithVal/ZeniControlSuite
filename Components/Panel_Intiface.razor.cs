using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ZeniControlSuite.Components;

public partial class Panel_Intiface : IDisposable
{
    public static bool pageEnabled = true;
    private string user = "Undefined";
    private string pageName = "Panel_Intiface";

    [Inject] private Service_Logs LogService { get; set; } = default!;
    [Inject] private Service_Intiface IntifaceService { get; set; } = default!;

    protected override void OnInitialized()
    {
        IntifaceService.OnIntifaceUpdate += OnIntifaceUpdate;
    }

    private void OnIntifaceUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        IntifaceService.OnIntifaceUpdate -= OnIntifaceUpdate;
    }

    public void EnableIntiface()
    {
        IntifaceService.EnableIntiface(LogService);
    }

    public void PowerFullStop()
    {
        IntifaceService.powerFullStop = !IntifaceService.powerFullStop;
    }

}