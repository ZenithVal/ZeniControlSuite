using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using MudBlazor;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components;

public partial class Panel_Intiface : IDisposable
{
    public static bool pageEnabled = true;
    private string user = "Undefined";
    private string pageName = "Panel_Intiface";

    [Inject] private Service_Logs LogService { get; set; } = default!;
    [Inject] private Service_Intiface IntifaceService { get; set; } = default!;

    //private ChartOptions chartOptions = new ChartOptions();

    protected override void OnInitialized()
    {
        IntifaceService.OnRequestDisplayUpdate += OnIntifaceUpdate;

        //chartOptions.InterpolationOption = InterpolationOption.NaturalSpline;
        //chartOptions.YAxisFormat = "c2";
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