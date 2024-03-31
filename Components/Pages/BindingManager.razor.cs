using Microsoft.AspNetCore.Components;
using static ZeniControlSuite.Components.BindingTreesService;

namespace ZeniControlSuite.Components.Pages;
public partial class BindingManager : IDisposable
{
    public static bool pageEnabled = true;

    [Inject]
    private GamesPointsService Points { get; set; } = default!;
    private BindingTreesService BindingTreesService { get; set; } = default!;

    protected override void OnInitialized()
    {
        Points.OnPointsUpdate += OnPointsUpdate;
        //BindingTreesService.OnBindingTreeUpdate += OnBindingTreeUpdate;
    }

    private void OnPointsUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    private void OnBindingTreeUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    private void btnHoverShowInfo(Binding binding)
    {
        //
    }

    private void btnBuyBinding(Binding binding)
    {
        //
    }

    private void btnSellBinding(Binding binding)
    {
        //
    }

    private void btnLockBinding(Binding binding)
    {
        //
    }

    public void Dispose()
    {
        Points.OnPointsUpdate -= OnPointsUpdate;
        //BindingTreesService.OnBindingTreeUpdate -= OnBindingTreeUpdate;
    }





}