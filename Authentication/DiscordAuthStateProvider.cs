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

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated == true && Whitelist.EnsureDiscordVisitor(user, out var userId))
        {
            var claims = GetClaims(user, userId);
            var identity = new ClaimsIdentity(claims, SuiteClaims.AuthenticationTypeDiscord);
            var principal = new ClaimsPrincipal(identity);

            AddUserToAcceptedList(userId);
            return Task.FromResult(new AuthenticationState(principal));
        }

        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal()));
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

        claims.RemoveAll(x => x.Type == "zcs:auth_mode");
        claims.Add(new Claim("zcs:auth_mode", SuiteClaims.AuthenticationTypeDiscord));

        return claims;
    }

    public static void AddUserToAcceptedList(string userID)
    {
        if (!Whitelist.usersAccepted.ContainsKey(userID) && Whitelist.usersToAccept.TryGetValue(userID, out var userInfo))
        {
            try
            {
                Whitelist.usersAccepted.Add(userID, new Whitelist.DiscordUser
                {
                    DisplayName = userInfo.DisplayName,
                    Roles = userInfo.Roles.ToList()
                });
            }
            catch { }
        }

        if (Whitelist.usersAccepted.ContainsKey(userID))
        {
            Console.WriteLine($"||| AUTH |||| User {Whitelist.usersAccepted[userID].DisplayName} authenticated");
        }
    }
}
