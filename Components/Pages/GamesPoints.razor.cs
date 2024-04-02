using Microsoft.AspNetCore.Components;
namespace ZeniControlSuite.Components.Pages;

using Microsoft.AspNetCore.Mvc.RazorPages;
using MudBlazor;

public partial class GamesPoints : IDisposable
{
    public static bool pageEnabled = true;
    private string user = "Undefined"; //Will later be replaced with the user's name via discord Auth
    private string pageName = "Games & Points";

    double third = 0.33333333; 
    double fourth = 0.25;

    [Inject] private GamesPointsService GPS { get; set; } = default!;
    [Inject] private LogService LogService { get; set; } = default!;

    protected override void OnInitialized()
    {
        GPS.OnGamesPointsUpdate += OnGamesPointsUpdate;
        LogService.AddLog(pageName, user, "PageLoad: Games & Points", Severity.Normal);
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

    private void btnPoints(double points)
    {
        GPS.UpdatePoints(points);
        string sign = points > 0 ? "+" : "";
        LogService.AddLog("PointsManual", "System", $"{sign}{points}p", Severity.Normal, Variant.Outlined);
    }

    #region Manual Buttons
    private void btnAddWhole()
    {
        btnPoints(1.0);
    }

    private void btnAddThird()
    {
        btnPoints(third);
    }

    private void btnAddFourth()
    {
        btnPoints(fourth);
    }

    private void btnSubWhole()
    {
        btnPoints(-1.0);
    }

    private void btnSubThird()
    {
        btnPoints(-third);
    }

    private void btnSubFourth()
    {
        btnPoints(-fourth);
    }
    #endregion

    #region Games
    #endregion

}