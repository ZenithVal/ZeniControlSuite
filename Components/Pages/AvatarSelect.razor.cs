using System.Drawing;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using MudBlazor.Utilities;
using ZeniControlSuite.Authentication;
using ZeniControlSuite.Models;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components.Pages;

public partial class AvatarSelect : IDisposable
{
    public static bool pageEnabled = true;

    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = default!;
    [Inject] private Service_Logs LogService { get; set; } = default!;
    [Inject] private Service_AvatarControls AvatarsService { get; set; } = default!;


    private string user = "Undefined";
    private string pageName = "AvatarSelect";

    protected override async Task OnInitializedAsync()
    {
        var context = await AuthProvider.GetAuthenticationStateAsync();
        user = context.GetUserName();

        AvatarsService.OnAvatarsUpdate += OnAvatarsUpdate;
    }

    private void OnAvatarsUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        AvatarsService.OnAvatarsUpdate -= OnAvatarsUpdate;
    }

    bool adminPanelExpand = false;
    private void ToggleAdminPanel()
    {
        adminPanelExpand = !adminPanelExpand;
    }

    private void SelectAvatar(Avatar avatar)
    {
        AvatarsService.SwitchAvatar(avatar);
        LogService.AddLog(pageName, user, $"Selected {avatar.Name}", Severity.Normal);
    }
}