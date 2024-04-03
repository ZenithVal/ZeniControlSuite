using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ZeniControlSuite.Components.Pages;

public partial class GamesPoints : IDisposable
{
    public static bool pageEnabled = true;
    private string user = "Undefined"; //Will later be replaced with the user's name via discord Auth
    private string pageName = "GamesPoints";

    [Inject] private LogService LogService { get; set; } = default!;
    [Inject] private PointsService PointsService { get; set; } = default!;
    [Inject] private GamesService GamesService { get; set; } = default!;

    protected override void OnInitialized()
    {
        LogService.AddLog(pageName, user, "PageLoad", Severity.Normal);
    }

    public void Dispose()
    {
    }
}