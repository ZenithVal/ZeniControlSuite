using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ZeniControlSuite.Components.Pages;

public partial class GamesPoints : IDisposable
{
    public static bool pageEnabled = true;
    private string user = "Undefined"; //Will later be replaced with the user's name via discord Auth
    private string pageName = "Games & Points";

    double third = 0.33333333; 
    double fourth = 0.25;

    [Inject] private GamesPointsService GPService { get; set; } = default!;
    [Inject] private LogService LogService { get; set; } = default!;

    protected override void OnInitialized()
    {
        localGame = GPService.gameSelected;
        GPService.OnGamesPointsUpdate += OnGamesPointsUpdate;
        LogService.AddLog(pageName, user, "PageLoad: Games & Points", Severity.Normal);
    }

    private void OnGamesPointsUpdate()
    {
        localGame = GPService.gameSelected;
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        GPService.OnGamesPointsUpdate -= OnGamesPointsUpdate;
    }

    #region Manual Buttons
    private void BtnModPoints(double points)
    {
        GPService.UpdatePoints(points);
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

    #region Games

    private Game localGame = new Game();

    private void ChangeGame()
    {
        if (GPService.AutoGameRunning)
        {
            LogService.AddLog(pageName, user, "Cannot Change Game While AutoGame is Running", Severity.Error, Variant.Outlined);
            return;
        }

        GPService.ChangeGame(localGame); 
        LogService.AddLog(pageName, user, $"Synced Game Changed to {localGame.Name}", Severity.Info, Variant.Outlined);
    }


    #endregion

}