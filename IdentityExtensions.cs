using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace ZeniControlSuite;

public static class IdentityExtensions
{
    public static string GetUserId(this AuthenticationState context) => context.User.Claims.FirstOrDefault(x=> x.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")!.Value;
    public static string GetUserName(this AuthenticationState context) =>  @context.User.Identity?.Name ?? "Unknown";
    public static string GetAvatarId(this AuthenticationState context) => context.User.Claims.FirstOrDefault(x=> x.Type == "urn:discord:avatar")!.Value;
    public static string GetAvatar(this AuthenticationState context) => $"https://cdn.discordapp.com/avatars/{context.GetUserId()}/{context.GetAvatarId()}";
}