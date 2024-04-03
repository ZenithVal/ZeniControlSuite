using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ZeniControlSuite.Components;

public partial class Panel_PointControls : IDisposable
{
    public static bool pageEnabled = true;
    private string user = "Undefined"; //Will later be replaced with the user's name via discord Auth
    private string pageName = "GamesPoints-PointControls";

    double third = 0.33333333;
    double fourth = 0.25;

    [Inject] private LogService LogService { get; set; } = default!;
    [Inject] private PointsService PointsService { get; set; } = default!;

    protected override void OnInitialized()
    {
        PointsService.OnPointsUpdate += OnPointsUpdate;
        LogService.AddLog(pageName, user, "PageLoad: Games & Points", Severity.Normal);
    }

    private void OnPointsUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        PointsService.OnPointsUpdate -= OnPointsUpdate;
    }

    #region Manual Buttons
    private void BtnModPoints(double points)
    {
        PointsService.UpdatePoints(points);
        string sign = points > 0 ? "+" : "";
        LogService.AddLog(pageName, user, $"{sign}{points}p", Severity.Info, Variant.Outlined);
    }

    private void btnAddWhole()
    {
        BtnModPoints(1.0);
    }

    private void btnAddThird()
    {
        BtnModPoints(third);
    }

    private void btnAddFourth()
    {
        BtnModPoints(fourth);
    }

    private void btnSubWhole()
    {
        BtnModPoints(-1.0);
    }

    private void btnSubThird()
    {
        BtnModPoints(-third);
    }

    private void btnSubFourth()
    {
        BtnModPoints(-fourth);
    }
    #endregion

}