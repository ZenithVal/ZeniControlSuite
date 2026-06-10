using System.Security.Claims;

namespace ZeniControlSuite.Authentication;

public static class SuiteClaims
{
    public const string AuthenticationTypeAdminPassword = "AdminPassword";
    public const string AuthenticationTypeVisitorCode = "VisitorCode";
    public const string AuthenticationTypeDiscord = "Discord";
    public const string AdminPasswordId = "admin-password";
    public const string VisitorCodeId = "visitor-code";

    public static ClaimsPrincipal CreateAdminPasswordPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, AdminPasswordId),
            new(ClaimTypes.Name, "Generated Admin"),
            new(ClaimTypes.Role, "Admin"),
            new(ClaimTypes.Role, "LocalHost"),
            new("zcs:auth_mode", AuthenticationTypeAdminPassword)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationTypeAdminPassword));
    }

    public static ClaimsPrincipal CreateVisitorCodePrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, VisitorCodeId),
            new(ClaimTypes.Name, "Visitor"),
            new(ClaimTypes.Role, "Visitor"),
            new("zcs:auth_mode", AuthenticationTypeVisitorCode)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationTypeVisitorCode));
    }

    public static bool IsLocalSuiteUser(ClaimsPrincipal principal)
    {
        return principal.HasClaim("zcs:auth_mode", AuthenticationTypeAdminPassword) ||
               principal.HasClaim("zcs:auth_mode", AuthenticationTypeVisitorCode) ||
               principal.FindFirstValue(ClaimTypes.NameIdentifier) is AdminPasswordId or VisitorCodeId;
    }

    public static bool IsAdminPasswordUser(ClaimsPrincipal principal)
    {
        return principal.HasClaim("zcs:auth_mode", AuthenticationTypeAdminPassword) ||
               principal.FindFirstValue(ClaimTypes.NameIdentifier) == AdminPasswordId;
    }

    public static bool IsVisitorCodeUser(ClaimsPrincipal principal)
    {
        return principal.HasClaim("zcs:auth_mode", AuthenticationTypeVisitorCode) ||
               principal.FindFirstValue(ClaimTypes.NameIdentifier) == VisitorCodeId;
    }
}
