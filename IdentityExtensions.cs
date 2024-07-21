using Microsoft.AspNetCore.Components.Authorization;
using Newtonsoft.Json;
using static ZeniControlSuite.Data.Service_Users;

namespace ZeniControlSuite;

public static class IdentityExtensions
{
    public static string GetUserId(this AuthenticationState context) => context.User.Claims.FirstOrDefault(x=> x.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")!.Value;
	public static string GetUserName(this AuthenticationState context) =>  @context.User.Identity?.Name ?? "Unknown";
	public static string GetAvatarId(this AuthenticationState context) => context.User.Claims.FirstOrDefault(x=> x.Type == "urn:discord:avatar")!.Value;
    public static string GetAvatar(this AuthenticationState context) => $"https://cdn.discordapp.com/avatars/{context.GetUserId()}/{context.GetAvatarId()}";
/*    public static List<string> GetRoles(this AuthenticationState context)
    {
        //im going insane
    }*/

    //Move to own service later, just here for learning.
    public static Dictionary<ulong, DiscordUser> discordUsersDict = new Dictionary<ulong, DiscordUser>();
    public static void loadDiscordUsersJson()
    {
        try
        {
            var json = File.ReadAllText("Configs/DiscordUsers.json");
            var users = JsonConvert.DeserializeObject<Dictionary<string, DiscordUser>>(json);
            foreach (var user in users)
            {
                discordUsersDict.Add(ulong.Parse(user.Key), user.Value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            if (!File.Exists("Configs/DiscordUsers.json"))
            {
                File.WriteAllText("Configs/DiscordUsers.json", "{}");
            }
        }
    }

    public static void saveDiscordUsersJson()
    {
        var json = JsonConvert.SerializeObject(discordUsersDict.ToDictionary(x => x.Key.ToString(), x => x.Value), Formatting.Indented);
        File.WriteAllText("Configs/DiscordUsers.json", json);
    }

    public static void AssignDiscordUsers(this DiscordUserClaim userClaim)
    {
        if (discordUsersDict.ContainsKey(userClaim.UserId))
        {
            var user = discordUsersDict[userClaim.UserId];
            userClaim.Name = user.DisplayName;
            userClaim.Roles = user.Roles;
        }
        else
        {
            discordUsersDict.Add(userClaim.UserId, new DiscordUser {
                DisplayName = userClaim.Name,
                Roles = new List<string>()
            });
        }
    }

}