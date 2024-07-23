using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace ZeniControlSuite.Authentication;

public class DiscordAuthStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    //private Service_Logs LogsService { get; set; } = default!; //causes crashes, f

    public DiscordAuthStateProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _httpContextAccessor.HttpContext.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var userID = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

			if (userID != null && Whitelist.usersToAccept.ContainsKey(userID))
			{
				var claims = GetClaims(user, userID);

				var identity = new ClaimsIdentity(claims, "Discord");
				var principal = new ClaimsPrincipal(identity);

				AddUserToAcceptedList(userID);

				return Task.FromResult(new AuthenticationState(principal)).Result;
			}
        }

		AddUserToDeniedList(user);
		return Task.FromResult(new AuthenticationState(new ClaimsPrincipal())).Result;
    }
	public static List<Claim> GetClaims(ClaimsPrincipal user, string userID)
	{
		var claims = new List<Claim>(user.Claims);

		claims.RemoveAll(x => x.Type == ClaimTypes.Name);
		claims.Add(new Claim(ClaimTypes.Name, Whitelist.usersToAccept[userID].DisplayName));

		var roles = Whitelist.usersToAccept[userID].Roles;
		claims.RemoveAll(x => x.Type == ClaimTypes.Role);
		foreach (var role in roles)
		{
			claims.Add(new Claim(ClaimTypes.Role, role));
		}

		return claims;
	}

    public static void AddUserToAcceptedList(string userID)
    {
		if (!Whitelist.usersAccepted.ContainsKey(userID))
		{
            var userInfo = Whitelist.usersToAccept[userID];
            Whitelist.usersAccepted.Add(userID, new Whitelist.DiscordUser { DisplayName = userInfo.DisplayName, Roles = userInfo.Roles });
			Console.WriteLine($"||| AUTH |||| User {Whitelist.usersAccepted[userID].DisplayName} authenticated");
		}

	}

    public static void AddUserToDeniedList(ClaimsPrincipal user)
    {
		var userID = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (user != null && userID != null && user.Identity.IsAuthenticated && !Whitelist.usersToAccept.ContainsKey(userID))
		{
			Console.WriteLine($"||| AUTH |||| User {userID} | {user.Identity.Name} is not registered, adding to denied users.");
			Whitelist.usersDenied.Add(userID, new Whitelist.DiscordUser { DisplayName = user.Identity.Name, Roles = new List<string>() });
			Whitelist.saveDeniedUsersJson();
		}
	}


}