using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace ZeniControlSuite.Authentication;

public static class IdentityExtensions
{
    public static string GetUserId(this AuthenticationState context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? SuiteClaims.VisitorCodeId;

    public static string GetUserName(this AuthenticationState context) =>
        context.User.Identity?.Name ?? "Unknown";

    public static string GetAvatarId(this AuthenticationState context) =>
        context.User.Claims.FirstOrDefault(x => x.Type == "urn:discord:avatar")?.Value ?? string.Empty;

    public static string GetAvatar(this AuthenticationState context)
    {
        var avatarId = context.GetAvatarId();
        var userId = context.GetUserId();
        return string.IsNullOrWhiteSpace(avatarId) || SuiteClaims.IsLocalSuiteUser(context.User)
            ? "/images/AvatarThumbDefault.png"
            : $"https://cdn.discordapp.com/avatars/{userId}/{avatarId}";
    }

    public static List<string> GetRoles(this AuthenticationState context)
    {
        return context.User.Claims
            .Where(x => x.Type == ClaimTypes.Role)
            .Select(x => x.Value)
            .ToList();
    }
}
