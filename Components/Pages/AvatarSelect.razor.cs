using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
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
    private readonly string pageName = "AvatarSelect";
    private bool adminPanelExpand;
    private bool avatarSelectEditMode;

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

    private void ToggleAdminPanel()
    {
        adminPanelExpand = !adminPanelExpand;
    }

    private void ToggleAvatarSelectEditMode()
    {
        avatarSelectEditMode = !avatarSelectEditMode;
    }

    private bool ShouldShowAvatarCard(Avatar avatar, bool isAdminUser)
    {
        if (isAdminUser && avatarSelectEditMode)
        {
            return true;
        }

        if (!avatar.Selectable)
        {
            return false;
        }

        if (!avatar.Available && !isAdminUser)
        {
            return false;
        }

        if (!AvatarsService.avatarSelectEnabled && !isAdminUser)
        {
            return false;
        }

        return true;
    }

    private bool CanSelectAvatar(Avatar avatar, bool isAdminUser)
    {
        if (isAdminUser)
        {
            return true;
        }

        return avatar.Selectable && avatar.Available && AvatarsService.avatarSelectEnabled;
    }

    private static string AvatarCardTitle(Avatar avatar)
    {
        return avatar.Name.Contains('|') ? avatar.Name.Split('|')[1].Trim() : avatar.Name;
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

        PointsService.UpdatePoints(-avatar.Cost * AvatarsService.avatarSelectCostMulti);
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
        Log("Trap Timer Increased", Severity.Normal);

        PointsService.UpdatePoints(-2);
    }

    private void DecreaseTrapTimer()
    {
        AvatarsService.TrapTimerUpdate(-15);
        Log("Trap Timer Decreased", Severity.Normal);

        PointsService.UpdatePoints(2);
    }

    private void UpdateAvatarName(Avatar avatar, string name)
    {
        AvatarsService.UpdateAvatarName(avatar, name);
    }

    private void UpdateAvatarIconName(Avatar avatar, string iconName)
    {
        AvatarsService.UpdateAvatarIconName(avatar, iconName);
    }

    private void MoveAvatar(Avatar avatar, int direction)
    {
        AvatarsService.MoveAvatar(avatar, direction);
    }

    private void UpdateAvatarCost(Avatar avatar, double cost)
    {
        AvatarsService.UpdateAvatarCost(avatar, cost);
    }

    private void UpdateAvatarSelectable(Avatar avatar, bool selectable)
    {
        AvatarsService.UpdateAvatarSelectable(avatar, selectable);
    }

    private void UpdateAvatarAvailable(Avatar avatar, bool available)
    {
        AvatarsService.UpdateAvatarAvailable(avatar, available);
    }

    private void UpdateAvatarSelectEnabled(bool enabled)
    {
        AvatarsService.avatarSelectEnabled = enabled;
        AvatarsService.SaveAvatarControls();
    }

    private void UpdateAvatarSelectFree(bool free)
    {
        AvatarsService.avatarSelectFree = free;
        AvatarsService.SaveAvatarControls();
    }

    private void UpdateAvatarTrapped(bool trapped)
    {
        AvatarsService.Trapped = trapped;
        AvatarsService.InvokeAvatarControlsUpdate();
    }

    private void UpdateAvatarCostMultiplier(double multiplier)
    {
        AvatarsService.avatarSelectCostMulti = Math.Max(1.0, multiplier);
        AvatarsService.InvokeAvatarControlsUpdate();
    }
}
