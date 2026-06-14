using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using MudBlazor;
using ZeniControlSuite.Authentication;
using ZeniControlSuite.Models;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components.Pages;

public partial class Shockers : IDisposable
{
    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = default!;
    [Inject] private Service_Logs LogService { get; set; } = default!;
    [Inject] private Service_OpenShock OpenShock { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly string pageName = "Shockers";
    private string user = "Undefined";
    private bool editMode;
    private DotNetObjectReference<Shockers>? _objectReference;

    private bool CanUseManualControls => OpenShock.CanControl && !OpenShock.CommandRunning;

    private string PrimaryShockerName => OpenShock.Config.Shockers.FirstOrDefault(shocker => shocker.Enabled)?.Name
        ?? OpenShock.Config.Shockers.FirstOrDefault()?.Name
        ?? "OpenShock";

    protected override async Task OnInitializedAsync()
    {
        OpenShock.OnOpenShockUpdate += OnOpenShockUpdate;
        var context = await AuthProvider.GetAuthenticationStateAsync();
        user = context.GetUserName();
        LogService.AddLog(pageName, user, "PageLoad", Severity.Normal);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            _objectReference ??= DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("zcsAvatarControls.bind", _objectReference);
        }
        catch (JSException ex)
        {
            LogService.AddLog(pageName, user, $"Radial control bind failed: {ex.Message}", Severity.Warning);
        }
    }

    private void OnOpenShockUpdate()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task SetRadialControlValue(string address, double value)
    {
        if (address.Equals("openshock:intensity", StringComparison.OrdinalIgnoreCase))
        {
            OpenShock.SetManualIntensity(value);
        }
        else if (address.Equals("openshock:duration", StringComparison.OrdinalIgnoreCase))
        {
            OpenShock.SetManualDuration(value);
        }

        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task SetHSVControlValue(string hueAddress, double hue, double saturation, double value)
    {
        return Task.CompletedTask;
    }

    private void ToggleEditMode()
    {
        editMode = !editMode;
    }

    private async Task SendWarningAsync()
    {
        await OpenShock.ManualWarningAsync();
    }

    private async Task SendShockAsync()
    {
        await OpenShock.ManualShockAsync();
    }

    private async Task CheckHubAsync()
    {
        await OpenShock.CheckHubStatusAsync();
    }

    private void SaveConfig()
    {
        OpenShock.SaveConfig();
        Snackbar.Add("OpenShock config saved.", Severity.Success);
    }

    private void ReloadConfig()
    {
        OpenShock.LoadConfig();
        Snackbar.Add("OpenShock config reloaded.", Severity.Info);
    }

    private void AddShocker()
    {
        OpenShock.AddShocker();
    }

    private void RemoveShocker(OpenShockDeviceConfig shocker)
    {
        OpenShock.RemoveShocker(shocker);
    }

    private void AddOscTrigger()
    {
        OpenShock.AddOscTrigger();
    }

    private void RemoveOscTrigger(OpenShockOscTriggerConfig trigger)
    {
        OpenShock.RemoveOscTrigger(trigger);
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        OpenShock.OnOpenShockUpdate -= OnOpenShockUpdate;
        _objectReference?.Dispose();
    }
}
