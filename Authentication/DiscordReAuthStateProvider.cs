using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace ZeniControlSuite.Authentication;

public class DiscordReAuthStateProvider : RevalidatingServerAuthenticationStateProvider
{
	private readonly IHttpContextAccessor _httpContextAccessor;

	public DiscordReAuthStateProvider(
		ILoggerFactory loggerFactory,
		IHttpContextAccessor httpContextAccessor)
		: base(loggerFactory)
	{
		_httpContextAccessor = httpContextAccessor;
	}

	protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

	protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
	{
		var user = authenticationState.User;

		if (user.Identity?.IsAuthenticated == true)
		{
			var userID = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

			if (userID != null && Whitelist.usersToAccept.ContainsKey(userID))
			{
				var claims = DiscordAuthStateProvider.GetClaims(user, userID);

				var identity = new ClaimsIdentity(claims, user.Identity.AuthenticationType);
				var principal = new ClaimsPrincipal(identity);
				var authState = new AuthenticationState(principal);

				NotifyAuthenticationStateChanged(Task.FromResult(authState));
				return true;
			}
		}
		return false;
	}


	public override Task<AuthenticationState> GetAuthenticationStateAsync()
	{
		var user = _httpContextAccessor.HttpContext?.User;

		if (user != null && user.Identity.IsAuthenticated)
		{
			return Task.FromResult(CheckWhitelistAndCreateState(user));
		}

		return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
	}

	private AuthenticationState CheckWhitelistAndCreateState(ClaimsPrincipal user)
	{
		var userID = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

		if (Whitelist.usersToAccept.ContainsKey(userID))
		{
			var claims = DiscordAuthStateProvider.GetClaims(user, userID);

			var identity = new ClaimsIdentity(claims, user.Identity.AuthenticationType);
			var principal = new ClaimsPrincipal(identity);

			return new AuthenticationState(principal);
		}

		return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
	}

	public void NotifyUserAuthentication(ClaimsPrincipal user)
	{
		var authState = Task.FromResult(CheckWhitelistAndCreateState(user));
		NotifyAuthenticationStateChanged(authState);
	}

	public void NotifyUserLogout()
	{
		var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
		var authState = Task.FromResult(new AuthenticationState(anonymousUser));
		NotifyAuthenticationStateChanged(authState);
	}
}