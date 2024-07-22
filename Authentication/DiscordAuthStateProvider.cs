using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace ZeniControlSuite.Authentication;

public class DiscordAuthStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

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
            //if there's a dictionary key with the userID, then the user is authenticated
            if (userID != null && Whitelist.acceptedMembers.ContainsKey(userID))
                {
                    var claims = new List<Claim>(user.Claims);
                    var roles = Whitelist.acceptedMembers[userID].Roles;

                    //Removes the default name claim and replaces it with the saved display name
                    claims.RemoveAll(x => x.Type == ClaimTypes.Name);
                    claims.Add(new Claim(ClaimTypes.Name, Whitelist.acceptedMembers[userID].DisplayName));

                    //add roles
                    foreach (var role in roles)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }

                    var identity = new ClaimsIdentity(claims, "Discord");
                    var principal = new ClaimsPrincipal(identity);

                    return Task.FromResult(new AuthenticationState(principal)).Result;
                }
            }

        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal())).Result;
    }
}