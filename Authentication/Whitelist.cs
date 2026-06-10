using System.Security.Claims;
using Newtonsoft.Json;

namespace ZeniControlSuite.Authentication;

public static class Whitelist
{
    public class DiscordUser
    {
        public string DisplayName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }

    public static Dictionary<string, DiscordUser> usersToAccept = new();
    public static Dictionary<string, DiscordUser> usersAccepted = new();

    public static void loadDiscordUsersJson()
    {
        try
        {
            Directory.CreateDirectory("Configs");
            usersToAccept.Clear();
            usersAccepted.Clear();

            if (!File.Exists("Configs/DiscordUsers.json"))
            {
                File.WriteAllText("Configs/DiscordUsers.json", "{}");
            }

            var json = File.ReadAllText("Configs/DiscordUsers.json");
            var users = JsonConvert.DeserializeObject<Dictionary<string, DiscordUser>>(json) ?? new Dictionary<string, DiscordUser>();
            foreach (var user in users)
            {
                usersToAccept[user.Key] = NormalizeUser(user.Value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred loading DiscordUsers.json: {ex.Message}");
        }
    }

    public static void saveDiscordUsersJson()
    {
        try
        {
            Directory.CreateDirectory("Configs");
            var json = JsonConvert.SerializeObject(usersToAccept, Formatting.Indented);
            File.WriteAllText("Configs/DiscordUsers.json", json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred saving DiscordUsers.json: {ex.Message}");
        }
    }

    public static bool EnsureDiscordVisitor(ClaimsPrincipal user, out string userId)
    {
        userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userId) || user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (!usersToAccept.ContainsKey(userId))
        {
            usersToAccept[userId] = new DiscordUser
            {
                DisplayName = user.Identity?.Name ?? user.FindFirst(ClaimTypes.Name)?.Value ?? userId,
                Roles = new List<string> { "Visitor" }
            };
            saveDiscordUsersJson();
            Console.WriteLine($"||| AUTH |||| User {userId} | {usersToAccept[userId].DisplayName} was added as Visitor.");
        }
        else
        {
            usersToAccept[userId] = NormalizeUser(usersToAccept[userId]);
        }

        return true;
    }

    private static DiscordUser NormalizeUser(DiscordUser user)
    {
        user.DisplayName ??= string.Empty;
        user.Roles ??= new List<string>();
        if (user.Roles.Count == 0)
        {
            user.Roles.Add("Visitor");
        }
        return user;
    }
}
