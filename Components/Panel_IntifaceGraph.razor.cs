using Microsoft.AspNetCore.Components;
using MudBlazor;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components;

public partial class Panel_IntifaceGraph : IDisposable
{
    public static bool pageEnabled = true;
    private string user = "Undefined";
    private string pageName = "Panel_Intiface_Graph";

    [Inject] private Service_Intiface IntifaceService { get; set; } = default!;

	private readonly ChartOptions IntifaceGraphOptions = new ChartOptions() {
		YAxisTicks = 1,
		YAxisLines = false,
		XAxisLines = false,
		ShowLegend = false,
		ShowLabels = false,
		ShowToolTips = false,
		ShowLegendLabels = false,
	};

	protected override void OnInitialized()
    {
        IntifaceService.OnIntifaceReadoutUpdate += OnIntifaceGraphUpdate;
    }

    private void OnIntifaceGraphUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        IntifaceService.OnIntifaceReadoutUpdate -= OnIntifaceGraphUpdate;
    }

}