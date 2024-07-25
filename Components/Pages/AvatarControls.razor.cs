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


    private void controlTogglePress(ContTypeToggle control)
    {
        if (control.Parameter.Value == control.ValueOff)
        {
            control.Parameter.Value = control.ValueOn;
        }
        else
        {
            control.Parameter.Value = control.ValueOff;
        }

        LogService.AddLog(pageName, user, $"{control.Name} toggled to {control.Parameter.Value}", Severity.Normal);
    }

}