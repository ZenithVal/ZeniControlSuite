using Microsoft.AspNetCore.Components;
using static ZeniControlSuite.Components.BindingTreesService;
namespace ZeniControlSuite.Components.Pages;

public partial class GamesPoints : IDisposable
{
    public static bool pageEnabled = true;

    [Inject] 
    private GamesPointsService GPS { get; set; } = default!;

    protected override void OnInitialized()
    {
        GPS.OnGamesPointsUpdate += OnGamesPointsUpdate;
    }
    private void OnGamesPointsUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        GPS.OnGamesPointsUpdate -= OnGamesPointsUpdate;
    }

    public void btnUpdate()
    {
        GPS.Update();
    }

    #region Manual Buttons
    private void btnAddWhole()
    {
        GPS.UpdatePoints(1.0);
    }

    private void btnAddThird()
    {
        GPS.UpdatePoints(1.0 / 3);
    }

    private void btnAddFourth()
    {
        GPS.UpdatePoints(1.0 / 4);
    }

    private void btnSubWhole()
    {
        GPS.UpdatePoints(-1);
    }

    private void btnSubThird()
    {
        GPS.UpdatePoints(-1.0 / 3);
    }

    private void btnSubFourth()
    {
        GPS.UpdatePoints(-1.0 / 4);
    }
    #endregion

    #region Games
    #endregion

}