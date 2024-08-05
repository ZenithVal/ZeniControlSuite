using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using ZeniControlSuite.Authentication;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components.Pages;

public partial class Devices : IDisposable
{
    public static bool pageEnabled = true;

    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = default!;
    [Inject] private Service_Logs LogService { get; set; } = default!;
    [Inject] private Service_Intiface IntifaceService { get; set; } = default!;

    private string user = "Undefined";
    private AuthenticationState context;
    private string pageName = "Devices";

    protected override async Task OnInitializedAsync()
    {
        var context = await AuthProvider.GetAuthenticationStateAsync();
        user = context.GetUserName();
        LogService.AddLog(pageName, user, "PageLoad", Severity.Normal);
    }

    public void Dispose()
    {
    }

}