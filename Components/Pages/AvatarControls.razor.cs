using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Globalization;
using MudBlazor;
using ZeniControlSuite.Authentication;
using ZeniControlSuite.Models;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Components.Pages;

public partial class AvatarControls : IDisposable
{
    public static bool pageEnabled = true;

    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = default!;
    [Inject] private Service_Logs LogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private Service_Avatars AvatarsService { get; set; } = default!;
    [Inject] private Service_OSC OscService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private string user = "Undefined";
    private bool isAdmin;
    private string avatarDisplayName = string.Empty;
    private string parameterFilter = string.Empty;
    private int newToggleAccessLevel;
    private int newToggleIntegerValue = 1;
    private bool loadingParameters;
    private bool editMode;
    private bool confirmDeleteAvatar;
    private DotNetObjectReference<AvatarControls>? jsObjectReference;
    private bool avatarControlsScriptWarningLogged;

    private readonly string pageName = "Avatar Controls";

    protected override async Task OnInitializedAsync()
    {
        AvatarsService.OnAvatarsUpdate += OnAvatarsUpdate;

        var context = await AuthProvider.GetAuthenticationStateAsync();
        user = context.GetUserName();
        isAdmin = context.User.IsInRole("Admin") || context.User.IsInRole("LocalHost");
        LogService.AddLog(pageName, user, "PageLoad", Severity.Normal);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        jsObjectReference ??= DotNetObjectReference.Create(this);

        try
        {
            await JSRuntime.InvokeVoidAsync("zcsAvatarControls.bind", jsObjectReference);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSException ex)
        {
            if (!avatarControlsScriptWarningLogged)
            {
                avatarControlsScriptWarningLogged = true;
                LogService.AddLog(pageName, user, $"Avatar controls JS unavailable: {ex.Message}", Severity.Warning);
            }
        }
    }

    private void OnAvatarsUpdate()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        AvatarsService.OnAvatarsUpdate -= OnAvatarsUpdate;
        jsObjectReference?.Dispose();
    }

    private void ToggleEditMode()
    {
        editMode = !editMode;
        confirmDeleteAvatar = false;
    }

    private void SetCurrentAccessLevel(int value)
    {
        AvatarsService.CurrentAccessLevel = Math.Clamp(value, 0, 10);
        AvatarsService.InvokeAvatarControlsUpdate();
    }

    private IEnumerable<AvatarControl> GetVisibleControls()
    {
        if (!AvatarsService.SelectedAvatarIsValid)
        {
            return Array.Empty<AvatarControl>();
        }

        var controls = AvatarsService.selectedAvatar.Controls;
        return isAdmin ? controls : controls.Where(CanUseControl);
    }

    private bool CanUseControl(AvatarControl control)
    {
        return isAdmin && editMode || control.AccessLevel <= AvatarsService.CurrentAccessLevel;
    }

    private string ControlCardStyle(AvatarControl control)
    {
        var enabled = CanUseControl(control);
        var color = enabled ? "rgba(76, 175, 80, .85)" : "rgba(244, 67, 54, .85)";
        var opacity = enabled ? "1" : ".45";
        return $"height: 100%; padding: 14px; border: 2px solid {color}; border-radius: 10px; opacity: {opacity};";
    }

    private bool TryGetParameter(Parameter parameter, out Parameter runtimeParameter)
    {
        if (AvatarsService.selectedAvatar.Parameters.TryGetValue(parameter.Address, out runtimeParameter!))
        {
            return true;
        }

        runtimeParameter = parameter;
        return false;
    }

    private void ControlTogglePress(ContTypeToggle control)
    {
        if (!CanUseControl(control))
        {
            Snackbar.Add($"{control.Name} requires access level {control.AccessLevel}.", Severity.Warning);
            return;
        }

        if (Math.Abs(control.Parameter.Value - control.ValueOff) < 0.001f)
        {
            control.Parameter.Value = control.ValueOn;
        }
        else
        {
            control.Parameter.Value = control.ValueOff;
        }

        AvatarsService.SetParameterValue(control.Parameter);
        LogService.AddLog(pageName, user, $"{control.Name} set to {control.Parameter.Value}", Severity.Normal);
    }

    private void ControlRadialChange(ContTypeRadial control, float value)
    {
        if (!CanUseControl(control))
        {
            return;
        }

        control.Parameter.Value = value;
        AvatarsService.SetParameterValue(control.Parameter);
    }

    private void ControlHSVChange(ContTypeHSV control, HSVParamValue param, float value)
    {
        if (!CanUseControl(control))
        {
            return;
        }

        switch (param)
        {
            case HSVParamValue.Hue:
                control.ParameterHue.Value = value;
                AvatarsService.SetParameterValue(control.ParameterHue);
                break;
            case HSVParamValue.Saturation:
                control.ParameterSaturation.Value = value;
                AvatarsService.SetParameterValue(control.ParameterSaturation);
                break;
            case HSVParamValue.Brightness:
                if (control.InvertedBrightness)
                {
                    control.InvertedBrightnessValue = value;
                    control.ParameterBrightness.Value = Math.Abs(1 - control.InvertedBrightnessValue);
                }
                else
                {
                    control.ParameterBrightness.Value = value;
                }
                AvatarsService.SetParameterValue(control.ParameterBrightness);
                break;
        }
    }

    [JSInvokable]
    public Task SetRadialControlValue(string address, double value)
    {
        if (!AvatarsService.SelectedAvatarIsValid)
        {
            return Task.CompletedTask;
        }

        var control = AvatarsService.selectedAvatar.Controls
            .OfType<ContTypeRadial>()
            .FirstOrDefault(radial => string.Equals(radial.Parameter.Address, address, StringComparison.OrdinalIgnoreCase));

        if (control == null || !CanUseControl(control))
        {
            return Task.CompletedTask;
        }

        var clampedValue = Math.Clamp((float)value, control.ValueMin, control.ValueMax);
        control.Parameter.Value = clampedValue;
        AvatarsService.SetParameterValue(control.Parameter, invokeUpdate: false);
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task SetHSVControlValue(string hueAddress, double hue, double saturation, double value)
    {
        if (!AvatarsService.SelectedAvatarIsValid)
        {
            return Task.CompletedTask;
        }

        var control = AvatarsService.selectedAvatar.Controls
            .OfType<ContTypeHSV>()
            .FirstOrDefault(hsv => string.Equals(hsv.ParameterHue.Address, hueAddress, StringComparison.OrdinalIgnoreCase));

        if (control == null || !CanUseControl(control))
        {
            return Task.CompletedTask;
        }

        var h = Math.Clamp((float)hue, 0f, 1f);
        var s = Math.Clamp((float)saturation, 0f, 1f);
        var v = Math.Clamp((float)value, 0f, 1f);

        control.ParameterHue.Value = h;
        control.ParameterSaturation.Value = s;
        if (control.InvertedBrightness)
        {
            control.InvertedBrightnessValue = v;
            control.ParameterBrightness.Value = Math.Abs(1f - v);
        }
        else
        {
            control.ParameterBrightness.Value = v;
        }

        AvatarsService.SetParameterValue(control.ParameterHue, invokeUpdate: false);
        AvatarsService.SetParameterValue(control.ParameterSaturation, invokeUpdate: false);
        AvatarsService.SetParameterValue(control.ParameterBrightness, invokeUpdate: false);
        return Task.CompletedTask;
    }

    private void LoadAvatarParameters()
    {
        loadingParameters = true;
        try
        {
            var result = AvatarsService.LoadCurrentAvatarParametersFromLocalFile();
            if (!string.IsNullOrWhiteSpace(result.AvatarName))
            {
                avatarDisplayName = result.AvatarName;
            }

            Snackbar.Add(result.Message, result.Success ? Severity.Success : Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Avatar parameter load failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            loadingParameters = false;
        }
    }

    private void AddCurrentAvatar()
    {
        AvatarsService.AddCurrentWornAvatarToControls(avatarDisplayName);
        avatarDisplayName = string.Empty;
        Snackbar.Add("Avatar added to controls.", Severity.Success);
    }

    private void MarkCurrentAvatarInvalid()
    {
        AvatarsService.MarkCurrentWornAvatarInvalid();
        Snackbar.Add("Avatar marked invalid.", Severity.Warning);
    }

    private void RemoveCurrentAvatarInvalidMark()
    {
        AvatarsService.RemoveCurrentWornAvatarFromInvalidList();
        Snackbar.Add("Avatar removed from invalid list.", Severity.Info);
    }

    private void DeleteSelectedAvatar()
    {
        if (!confirmDeleteAvatar)
        {
            confirmDeleteAvatar = true;
            Snackbar.Add("Press Confirm Delete to remove this avatar from controls.", Severity.Warning);
            return;
        }

        var deletedName = AvatarsService.selectedAvatar.Name;
        if (AvatarsService.DeleteSelectedAvatarFromControls())
        {
            Snackbar.Add($"Deleted {deletedName} from avatar controls.", Severity.Warning);
        }
        else
        {
            Snackbar.Add("Avatar could not be deleted.", Severity.Error);
        }

        confirmDeleteAvatar = false;
    }

    private void CancelDeleteSelectedAvatar()
    {
        confirmDeleteAvatar = false;
    }

    private void AddMatchedGlobals()
    {
        var added = AvatarsService.AddMatchedGlobalControlsToSelected();
        Snackbar.Add($"Added {added} matched global control(s).", added > 0 ? Severity.Success : Severity.Info);
    }

    private void AddGlobalControl(AvatarControl control)
    {
        AvatarsService.AddGlobalControlToSelected(control);
        Snackbar.Add($"Added {control.Name}.", Severity.Success);
    }

    private void AddLoadedToggle(DiscoveredOscParameter parameter)
    {
        AvatarsService.AddToggleFromDiscoveredParameter(parameter, newToggleAccessLevel, integerOnValue: newToggleIntegerValue);
        Snackbar.Add($"Added {parameter.DisplayName} to this avatar.", Severity.Success);
    }

    private void AddLoadedToggleToGlobal(DiscoveredOscParameter parameter)
    {
        AvatarsService.AddToggleFromDiscoveredParameterToGlobal(parameter, newToggleAccessLevel, integerOnValue: newToggleIntegerValue);
        Snackbar.Add($"Added {parameter.DisplayName} to global controls.", Severity.Success);
    }

    private void AdjustControlAccess(AvatarControl control, int direction)
    {
        AvatarsService.SetControlAccessLevel(control, control.AccessLevel + direction);
    }

    private void RenameControl(AvatarControl control, string name)
    {
        AvatarsService.RenameControl(control, name);
    }

    private void SetControlIconName(AvatarControl control, string iconName)
    {
        AvatarsService.SetControlIconName(control, iconName);
    }

    private void InvertToggleValues(ContTypeToggle control)
    {
        AvatarsService.InvertToggleValues(control);
        Snackbar.Add($"Inverted {control.Name}.", Severity.Info);
    }

    private void SetIntegerToggleValue(ContTypeToggle control, int value)
    {
        AvatarsService.SetToggleIntegerValue(control, value);
    }

    private int ToggleOnValueAsInt(ContTypeToggle control)
    {
        return (int)MathF.Round(control.ValueOn);
    }

    private void MoveSelectedControl(AvatarControl control, int direction)
    {
        AvatarsService.MoveSelectedControl(control, direction);
    }

    private void MoveGlobalControl(AvatarControl control, int direction)
    {
        AvatarsService.MoveGlobalControl(control, direction);
    }

    private void AddOrSyncControlToGlobal(AvatarControl control)
    {
        var syncExisting = AvatarsService.SelectedControlHasGlobalNameMatch(control);
        AvatarsService.AddOrSyncSelectedControlToGlobal(control);
        Snackbar.Add(syncExisting ? $"Synced {control.Name} to global controls and linked it." : $"Added {control.Name} to global controls and linked it.", Severity.Success);
    }

    private void AddControlToGlobal(AvatarControl control)
    {
        AddOrSyncControlToGlobal(control);
    }

    private void SetControlAccessLevel(AvatarControl control, int accessLevel)
    {
        AvatarsService.SetControlAccessLevel(control, accessLevel);
    }

    private void BreakGlobalControl(AvatarControl control)
    {
        AvatarsService.BreakSelectedControlGlobalLink(control);
        Snackbar.Add($"{control.Name} is now unique to this avatar.", Severity.Info);
    }

    private void RemoveSelectedControl(AvatarControl control)
    {
        AvatarsService.RemoveSelectedControl(control);
        Snackbar.Add($"Removed {control.Name} from this avatar.", Severity.Warning);
    }

    private void RemoveGlobalControl(AvatarControl control)
    {
        AvatarsService.RemoveGlobalControl(control);
        Snackbar.Add($"Removed {control.Name} from global controls.", Severity.Warning);
    }

    private IEnumerable<DiscoveredOscParameter> FilteredLoadedParameters()
    {
        var parameters = OscService.DiscoveredAvatarParameters
            .Where(parameter => parameter.Type is ParameterType.Bool or ParameterType.Int)
            .Where(parameter => parameter.Address.StartsWith("/avatar/parameters/", StringComparison.OrdinalIgnoreCase))
            .Where(parameter => !SelectedAvatarHasParameter(parameter.Address));

        if (!string.IsNullOrWhiteSpace(parameterFilter))
        {
            parameters = parameters.Where(parameter => parameter.DisplayName.Contains(parameterFilter, StringComparison.OrdinalIgnoreCase)
                || parameter.Address.Contains(parameterFilter, StringComparison.OrdinalIgnoreCase));
        }

        return parameters.OrderBy(parameter => parameter.DisplayName);
    }

    private bool SelectedAvatarHasParameter(string address)
    {
        return AvatarsService.selectedAvatar.Parameters.ContainsKey(address)
            || AvatarsService.selectedAvatar.Controls.Any(control => TryControlHasParameter(control, address));
    }

    private static bool TryControlHasParameter(AvatarControl control, string address)
    {
        return control switch
        {
            ContTypeButton button => string.Equals(button.Parameter.Address, address, StringComparison.OrdinalIgnoreCase),
            ContTypeToggle toggle => string.Equals(toggle.Parameter.Address, address, StringComparison.OrdinalIgnoreCase),
            ContTypeRadial radial => string.Equals(radial.Parameter.Address, address, StringComparison.OrdinalIgnoreCase),
            ContTypeHSV hsv => string.Equals(hsv.ParameterHue.Address, address, StringComparison.OrdinalIgnoreCase)
                || string.Equals(hsv.ParameterSaturation.Address, address, StringComparison.OrdinalIgnoreCase)
                || string.Equals(hsv.ParameterBrightness.Address, address, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private string Invariant(float value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private string BoolAttr(bool value)
    {
        return value ? "true" : "false";
    }

    private float RoundFloat(float value)
    {
        return (float)Math.Round(value, 3);
    }
}
