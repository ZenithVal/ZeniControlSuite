using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components;

public partial class Panel_IntifaceReadout : IDisposable, IAsyncDisposable
{
    public static bool pageEnabled = true;

    [Inject] private Service_Intiface IntifaceService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private ElementReference graphCanvas;
    private IJSObjectReference? graphModule;
    private bool graphReady;
    private string OutputPercent => $"{IntifaceService.PowerOutput:P0}";

    protected override void OnInitialized()
    {
        IntifaceService.OnIntifaceReadoutUpdate += OnIntifaceReadoutUpdate;
        IntifaceService.OnIntifaceGraphUpdate += OnIntifaceGraphUpdate;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            graphModule = await JS.InvokeAsync<IJSObjectReference>("import", "/zcs-intiface-graph.js");
            await graphModule.InvokeVoidAsync("start", graphCanvas);
            graphReady = true;
            await PushGraphValue();
        }
        catch
        {
            graphReady = false;
        }
    }

    private void OnIntifaceReadoutUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    private void OnIntifaceGraphUpdate()
    {
        _ = InvokeAsync(PushGraphValue);
    }

    private async Task PushGraphValue()
    {
        if (!graphReady || graphModule == null)
        {
            return;
        }

        try
        {
            await graphModule.InvokeVoidAsync("setValue", graphCanvas, IntifaceService.PowerOutput);
        }
        catch
        {
            graphReady = false;
        }
    }

    public void Dispose()
    {
        IntifaceService.OnIntifaceReadoutUpdate -= OnIntifaceReadoutUpdate;
        IntifaceService.OnIntifaceGraphUpdate -= OnIntifaceGraphUpdate;
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        if (graphModule == null)
        {
            return;
        }

        try
        {
            await graphModule.InvokeVoidAsync("stop", graphCanvas);
            await graphModule.DisposeAsync();
        }
        catch
        {
            // Circuit/browser shutdown can dispose JS before this component finalizes.
        }
    }
}
