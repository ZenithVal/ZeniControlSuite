using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components;

public partial class Panel_GameSelector : IDisposable
{
    public static bool pageEnabled = true;

    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = default!;
    [Inject] private Service_Logs LogService { get; set; } = default!;
    [Inject] private Service_Games GamesService { get; set; } = default!;


    private string user = "Undefined";
    private string pageName = "GameSelector";

    private Game localGame = new Game();


    protected override async Task OnInitializedAsync()
    {
        var context = await AuthProvider.GetAuthenticationStateAsync();
        user = context.GetUserName();

        localGame = GamesService.gameSelected;
        GamesService.OnGamesUpdate += OnGamesUpdate;
    }

    private void OnGamesUpdate()
    {
        if (GamesService.AutoGameRunning && localGame != GamesService.gameSelected) localGame = GamesService.gameSelected;
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        GamesService.OnGamesUpdate -= OnGamesUpdate;
    }

    private void ChangeGame()
    {
        if (GamesService.AutoGameRunning)
        {
            LogService.AddLog(pageName, user, "Cannot change game while autoGame is running", Severity.Error, Variant.Outlined);
            return;
        }

        GamesService.ChangeGame(localGame);
        LogService.AddLog(pageName, user, $"Synced Game changed to {GamesService.gameSelected.Name}", Severity.Info, Variant.Outlined);
    }

}