using Microsoft.AspNetCore.Components;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components;

public partial class Panel_IntifaceReadout : IDisposable
{
    public static bool pageEnabled = true;
    private string user = "Undefined";
    private string pageName = "Panel_Intiface";

    [Inject] private Service_Intiface IntifaceService { get; set; } = default!;

    protected override void OnInitialized()
    {
        IntifaceService.OnIntifaceReadoutUpdate += OnIntifaceReadoutUpdate;
    }

    private void OnIntifaceReadoutUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        IntifaceService.OnIntifaceReadoutUpdate -= OnIntifaceReadoutUpdate;
    }

}