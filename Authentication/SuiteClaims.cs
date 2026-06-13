using System.Security.Claims;
using System.Threading;

namespace ZeniControlSuite.Authentication;

public static class SuiteClaims
{
    public const string AuthenticationTypeAdminPassword = "AdminPassword";
    public const string AuthenticationTypeVisitorCode = "VisitorCode";
    public const string AuthenticationTypeDiscord = "Discord";
    public const string AdminPasswordId = "admin-password";
    public const string VisitorCodeId = "visitor-code";
    public const string AuthModeClaim = "zcs:auth_mode";
    public const string RuntimeSessionClaim = "zcs:runtime_session";
    public const string VisitorNumberClaim = "zcs:visitor_number";

    private static readonly string RuntimeSessionId = Guid.NewGuid().ToString("N");
    private static int _nextVisitorNumber;

    public static int NextVisitorNumber()
    {
        return Interlocked.Increment(ref _nextVisitorNumber);
    }

    public static ClaimsPrincipal CreateAdminPasswordPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, AdminPasswordId),
            new(ClaimTypes.Name, "Generated Admin"),
            new(ClaimTypes.Role, "Admin"),
            new(ClaimTypes.Role, "LocalHost"),
            new(AuthModeClaim, AuthenticationTypeAdminPassword),
            new(RuntimeSessionClaim, RuntimeSessionId)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationTypeAdminPassword));
    }

    public static ClaimsPrincipal CreateVisitorCodePrincipal(int visitorNumber)
    {
        var visitorName = $"Visitor {visitorNumber}";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"{VisitorCodeId}-{visitorNumber}"),
            new(ClaimTypes.Name, visitorName),
            new(ClaimTypes.Role, "Visitor"),
            new(AuthModeClaim, AuthenticationTypeVisitorCode),
            new(RuntimeSessionClaim, RuntimeSessionId),
            new(VisitorNumberClaim, visitorNumber.ToString())
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationTypeVisitorCode));
    }

    public static bool HasLocalSuiteIdentity(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var authMode = principal.FindFirstValue(AuthModeClaim);
        if (string.Equals(authMode, AuthenticationTypeAdminPassword, StringComparison.Ordinal) ||
            string.Equals(authMode, AuthenticationTypeVisitorCode, StringComparison.Ordinal))
        {
            return true;
        }

        var authenticationType = principal.Identity.AuthenticationType;
        if (string.Equals(authenticationType, AuthenticationTypeAdminPassword, StringComparison.Ordinal) ||
            string.Equals(authenticationType, AuthenticationTypeVisitorCode, StringComparison.Ordinal))
        {
            return true;
        }

        var identifier = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return identifier == AdminPasswordId ||
               identifier == VisitorCodeId ||
               (identifier?.StartsWith(VisitorCodeId + "-", StringComparison.Ordinal) == true);
    }

    public static bool IsLocalSuiteUser(ClaimsPrincipal principal)
    {
        return HasLocalSuiteIdentity(principal) &&
               string.Equals(principal.FindFirstValue(RuntimeSessionClaim), RuntimeSessionId, StringComparison.Ordinal);
    }

    public static bool IsAdminPasswordUser(ClaimsPrincipal principal)
    {
        return IsLocalSuiteUser(principal) &&
               (principal.HasClaim(AuthModeClaim, AuthenticationTypeAdminPassword) ||
                principal.FindFirstValue(ClaimTypes.NameIdentifier) == AdminPasswordId);
    }

    public static bool IsVisitorCodeUser(ClaimsPrincipal principal)
    {
        return IsLocalSuiteUser(principal) &&
               (principal.HasClaim(AuthModeClaim, AuthenticationTypeVisitorCode) ||
                principal.FindFirstValue(ClaimTypes.NameIdentifier)?.StartsWith(VisitorCodeId, StringComparison.Ordinal) == true);
    }
}
