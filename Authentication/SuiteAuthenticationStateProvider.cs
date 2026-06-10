using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace ZeniControlSuite.Authentication;

public sealed class SuiteAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SuiteAuthenticationStateProvider(ILoggerFactory loggerFactory, IHttpContextAccessor httpContextAccessor)
        : base(loggerFactory)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(15);

    protected override Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        var principal = NormalizePrincipal(authenticationState.User);
        return Task.FromResult(principal.Identity?.IsAuthenticated == true);
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return Task.FromResult(new AuthenticationState(NormalizePrincipal(user)));
    }

    private static ClaimsPrincipal NormalizePrincipal(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        if (SuiteClaims.IsLocalSuiteUser(user))
        {
            return user;
        }

        var userID = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(userID) && Whitelist.usersToAccept.ContainsKey(userID))
        {
            return new ClaimsPrincipal(new ClaimsIdentity(DiscordAuthStateProvider.GetClaims(user, userID), SuiteClaims.AuthenticationTypeDiscord));
        }

        return new ClaimsPrincipal(new ClaimsIdentity());
    }
}
