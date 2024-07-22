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

        if (user != null && user.Identity.IsAuthenticated)
        {
            var userID = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            //If user is in toAccept list, they need to be updated.
            if (userID != null && Whitelist.usersToAccept.ContainsKey(userID))
            {
                var claims = new List<Claim>(user.Claims);

                //Removes the default name claim and replaces it with the saved display name
                claims.RemoveAll(x => x.Type == ClaimTypes.Name); 
                claims.Add(new Claim(ClaimTypes.Name, Whitelist.usersToAccept[userID].DisplayName));

                var roles = Whitelist.usersToAccept[userID].Roles;
                claims.RemoveAll(x => x.Type == ClaimTypes.Role); //remove old roles
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                var identity = new ClaimsIdentity(claims, "Discord");
                var principal = new ClaimsPrincipal(identity);

                if (!Whitelist.usersAccepted.ContainsKey(userID))
                {
                    try { Whitelist.usersAccepted.Add(userID, new Whitelist.DiscordUser { DisplayName = Whitelist.usersToAccept[userID].DisplayName, Roles = roles }); }
                    catch { } //if a page has multiple requests, it will throw an error because it adds the same item to the dictionary
                    //LogsService.AddLog("Authentication", "System", $"User {Whitelist.usersAccepted[userID].DisplayName} authenticated", Severity.Info, Variant.Outlined);
                    Console.WriteLine($"||| AUTH |||| User {Whitelist.usersAccepted[userID].DisplayName} authenticated");
                }

                return Task.FromResult(new AuthenticationState(principal)).Result;
            }
        }


        if (user.Identity.IsAuthenticated)
        {
            var userID = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            //LogsService.AddLog("Authentication", "System", $"User {userID} | {user.Identity.Name} failed to authenticate", Severity.Warning, Variant.Outlined);
            Console.WriteLine($"||| AUTH |||| User {userID} | {user.Identity.Name} is not registered, adding to denied users.");
            Whitelist.usersDenied.Add(userID, new Whitelist.DiscordUser { DisplayName = user.Identity.Name, Roles = new List<string>() });
            Whitelist.saveDeniedUsersJson();
        }

        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal())).Result;
    }
}