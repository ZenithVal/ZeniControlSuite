using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using ZeniControlSuite.Models.AvatarControls;
using ZeniControlSuite.Services;
using static ZeniControlSuite.Services.Service_BindingTrees;

namespace ZeniControlSuite.Components.Pages;

public partial class AvatarControls : IDisposable
{
	public static bool pageEnabled = true;

	[Inject] private AuthenticationStateProvider AuthProvider { get; set; } = default!;
	[Inject] private Service_Logs LogService { get; set; } = default!;
	[Inject] private ISnackbar Snackbar { get; set; } = default!;

	[Inject] private Service_AvatarControls AvatarsService { get; set; } = default!;

	private string user = "Undefined";
	private AuthenticationState context;
	private readonly string pageName = "Avatar Controls";

	protected override async Task OnInitializedAsync()
	{
		AvatarsService.OnAvatarsUpdate += OnAvatarsUpdate;

		var context = await AuthProvider.GetAuthenticationStateAsync();
		user = context.GetUserName();
		LogService.AddLog(pageName, user, "PageLoad", Severity.Normal);

	}
	private void OnAvatarsUpdate()
	{
		InvokeAsync(StateHasChanged);
	}

	public void Dispose()
	{
		AvatarsService.OnAvatarsUpdate -= OnAvatarsUpdate;
	}


    private void ControlTogglePress(ContTypeToggle control)
    {
        if (control.Parameter.Value == control.ValueOff)
        {
            control.Parameter.Value = control.ValueOn;
        }
        else
        {
            control.Parameter.Value = control.ValueOff;
        }
		AvatarsService.InvokeAvatarControlsUpdate();

        LogService.AddLog(pageName, user, $"toggled {control.Name} set to {control.Parameter.Value}", Severity.Normal);
    }

	private void ControlRadialChange(ContTypeRadial control, float value)
	{
		control.Parameter.Value = value;
        AvatarsService.InvokeAvatarControlsUpdate();
        //LogService.AddLog(pageName, user, $"radial {control.Name} set to {control.Parameter.Value}", Severity.Normal);
    }

	private void ControlHSVChange(ContTypeHSV control, MudBlazor.Utilities.MudColor targetColor)
	{
        control.targetColor = targetColor;

        control.ParameterHue.Value = (float)control.targetColor.H/360;
        control.ParameterSaturation.Value = (float)control.targetColor.S;
		if (control.InvertedBrightness)
		{
			control.ParameterBrightness.Value = 1.0f - (float)control.targetColor.L;
		}
		else
		{
            control.ParameterBrightness.Value = (float)control.targetColor.L;
        }
        
        AvatarsService.InvokeAvatarControlsUpdate();
        LogService.AddLog(pageName, user, $"hsv {control.Name} set to {control.ParameterHue.Value}, {control.ParameterSaturation.Value}, {control.ParameterBrightness.Value}", Severity.Normal);
    }

}