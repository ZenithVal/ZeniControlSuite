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
    private string pageName = "Devices";
    private bool isAdmin;

    private bool CanShowIntifacePanel => isAdmin || IntifaceService.IntifaceRunning;

    protected override async Task OnInitializedAsync()
    {
        IntifaceService.OnIntifaceControlsUpdate += OnIntifaceControlsUpdate;

        var context = await AuthProvider.GetAuthenticationStateAsync();
        user = context.GetUserName();
        isAdmin = context.User.IsInRole("Admin");
        LogService.AddLog(pageName, user, "PageLoad", Severity.Normal);
    }

    private void OnIntifaceControlsUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        IntifaceService.OnIntifaceControlsUpdate -= OnIntifaceControlsUpdate;
    }
}
