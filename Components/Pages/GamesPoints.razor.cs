using Microsoft.AspNetCore.Components;
using static ZeniControlSuite.Components.GamesPointsService;

namespace ZeniControlSuite.Components.Pages;

public partial class GamesPoints : IDisposable
{
    public static bool pageEnabled = true;

    [Inject]
    private GamesPointsService Points { get; set; } = default!;

    protected override void OnInitialized()
    {
        Points.OnPointsUpdate += OnPointsUpdate;
    }

    private void OnPointsUpdate()
    {
        InvokeAsync(StateHasChanged);
    }
    public void Dispose()
    {
        Points.OnPointsUpdate -= OnPointsUpdate;
    }

    #region Manual Buttons
    private void btnAddWhole()
    {
        Points.UpdatePoints(1.0);
    }

    private void btnAddThird()
    {
        Points.UpdatePoints(1.0 / 3);
    }

    private void btnAddFourth()
    {
        Points.UpdatePoints(1.0 / 4);
    }

    private void btnSubWhole()
    {
        Points.UpdatePoints(-1);
    }

    private void btnSubThird()
    {
        Points.UpdatePoints(-1.0 / 3);
    }

    private void btnSubFourth()
    {
        Points.UpdatePoints(-1.0 / 4);
    }
    #endregion


    #region Games

    #endregion

}