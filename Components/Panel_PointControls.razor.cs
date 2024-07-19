using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components;

public partial class Panel_PointControls : IDisposable
{
    double third = 0.333;
    double fourth = 0.25;

    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = default!;
    [Inject] private Service_Logs LogService { get; set; } = default!;
    [Inject] private Service_Points PointsService { get; set; } = default!;

    private string user = "Undefined"; //Will later be replaced with the user's name via discord Auth
    private AuthenticationState context;
    private string pageName = "PointControls";

    protected override async Task OnInitializedAsync()
    {
        PointsService.OnPointsUpdate += OnPointsUpdate;

        var context = await AuthProvider.GetAuthenticationStateAsync();
        user = context.GetUserName();
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
        string sign = points > 0 ? "Added +" : "Removed ";
        LogService.AddLog(pageName, user, $"{sign}{points}p | Total: {PointsService.pointsTruncated}", Severity.Info, Variant.Outlined);
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