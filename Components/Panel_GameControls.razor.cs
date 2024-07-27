using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using ZeniControlSuite.Extensions;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components;

public partial class Panel_GameControls : IDisposable
{
    public static bool pageEnabled = true;
    private string user = "Undefined";
    private string pageName = "GameControls";

    private string localPlayerName = "";
    private string remotePlayerName = "";

    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = default!;
    [Inject] private Service_Logs LogService { get; set; } = default!;
    [Inject] private Service_Points PointsService { get; set; } = default!;
    [Inject] private Service_Games GamesService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        localPlayerName = GamesService.localPlayerName;
        remotePlayerName = GamesService.remotePlayerName;

        PointsService.OnPointsUpdate += OnPointsUpdate;
        GamesService.OnGamesUpdate += OnGamesUpdate;

        var context = await AuthProvider.GetAuthenticationStateAsync();
        user = context.GetUserName();
    }

    private void OnPointsUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    private void OnGamesUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    private void ChangeNames()
    {
        if (GamesService.AutoGameRunning)
        {
            LogService.AddLog(pageName, user, "Cannot change usernames while autoGame is running", Severity.Error, Variant.Outlined);
            return;
        }

        GamesService.ChangeNames(localPlayerName, remotePlayerName);
        LogService.AddLog(pageName, user, $"Usernames changed: {localPlayerName} & {remotePlayerName} ", Severity.Info, Variant.Outlined);
    }

    private void AutoGameStart()
    {
        if (GamesService.AutoGameRunning) return;

        else
        {
            GamesService.AG_Start();
            LogService.AddLog(pageName, user, "AutoGame started", Severity.Info, Variant.Outlined);
        }
    }

    private void AutoGameStop()
    {
        if (!GamesService.AutoGameRunning) return;

        else
        {
            GamesService.AG_Stop();
            LogService.AddLog(pageName, user, "AutoGame stopped", Severity.Info, Variant.Outlined);
        }
    }

    public void Dispose()
    {
        PointsService.OnPointsUpdate -= OnPointsUpdate;
        GamesService.OnGamesUpdate -= OnGamesUpdate;
    }

}