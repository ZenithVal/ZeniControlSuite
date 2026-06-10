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
    public static Dictionary<string, DiscordUser> usersDenied = new();

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
                usersToAccept[user.Key] = user.Value;
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

    public static void saveDeniedUsersJson()
    {
        try
        {
            Directory.CreateDirectory("Configs");
            var json = JsonConvert.SerializeObject(usersDenied, Formatting.Indented);
            File.WriteAllText("Configs/DeniedDiscordUsers.json", json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred saving DeniedDiscordUsers.json: {ex.Message}");
        }
    }
}
