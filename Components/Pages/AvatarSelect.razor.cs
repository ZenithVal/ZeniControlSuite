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
    [Inject] private Service_Avatars AvatarsService { get; set; } = default!;
    [Inject] private Service_Points PointsService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;


    private string user = "Undefined";
    private string pageName = "AvatarSelect";

    protected override async Task OnInitializedAsync()
    {
        PointsService.OnPointsUpdate += OnPointsUpdate;
        AvatarsService.OnAvatarsUpdate += OnAvatarsUpdate;

        var context = await AuthProvider.GetAuthenticationStateAsync();
        user = context.GetUserName();
        Log("PageLoad", Severity.Normal);
    }

    private void OnPointsUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    private void OnAvatarsUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        PointsService.OnPointsUpdate -= OnPointsUpdate;
        AvatarsService.OnAvatarsUpdate -= OnAvatarsUpdate;
    }

    private void Log(string message, Severity severity)
    {
        LogService.AddLog(pageName, user, message, severity);
        Snackbar.Add(message, severity);
    }

    bool adminPanelExpand = false;
    private void ToggleAdminPanel()
    {
        adminPanelExpand = !adminPanelExpand;
    }

    private void SelectAvatar(Avatar avatar)
    {
        AvatarsService.SwitchAvatar(avatar);
        Log($"Selected {avatar.Name}", Severity.Normal);
    }

    private void PurchaseAvatar(Avatar avatar)
    {
        if (AvatarsService.selectedAvatar != avatar)
        {
            AvatarsService.SwitchAvatar(avatar);
            Log($"Bought Select {avatar.Name}", Severity.Normal);
        }
        else
        {
            Log($"Already Selected {avatar.Name}, increasing trap timer", Severity.Normal);
        }
        
        //AvatarsService.TrapAvatar();
        PointsService.UpdatePoints(-avatar.Cost*AvatarsService.avatarSelectCostMulti);
    }

    private void IncreaseTrapTimer()
    {
        if (!AvatarsService.Trapped)
        {
            AvatarsService.TrapAvatar();
        }
        else
        {
            AvatarsService.TrapTimerUpdate(15);
        }
        Log($"Trap Timer Increased", Severity.Normal);

        PointsService.UpdatePoints(-2);
    }

    private void DecreaseTrapTimer()
    {
        AvatarsService.TrapTimerUpdate(-15);
        Log($"Trap Timer Decreased", Severity.Normal);

        PointsService.UpdatePoints(2);
    }
}