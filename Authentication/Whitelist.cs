using Newtonsoft.Json;

namespace ZeniControlSuite.Authentication;

public static class Whitelist
{
    public class DiscordUser
    {
        //public string ID { get; set; }
        public string DisplayName { get; set; }
        public List<string> Roles { get; set; }
    }

    public static Dictionary<string, DiscordUser> usersToAccept = new Dictionary<string, DiscordUser>();

    public static Dictionary<string, DiscordUser> usersAccepted = new Dictionary<string, DiscordUser>();

    public static Dictionary<string, DiscordUser> usersDenied = new Dictionary<string, DiscordUser>();

    public static void loadDiscordUsersJson()
    {
        try
        {
            usersToAccept.Clear();
            usersAccepted.Clear();
            var json = File.ReadAllText("Configs/DiscordUsers.json");
            var users = JsonConvert.DeserializeObject<Dictionary<string, DiscordUser>>(json);
            foreach (var user in users)
            {
                Whitelist.usersToAccept.Add(user.Key, user.Value);
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
        try
        {
            var json = JsonConvert.SerializeObject(usersToAccept, Formatting.Indented);
            File.WriteAllText("Configs/DiscordUsers.json", json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public static void saveDeniedUsersJson()
    {
        try
        {
            var json = JsonConvert.SerializeObject(usersDenied, Formatting.Indented);
            File.WriteAllText("Configs/DeniedDiscordUsers.json", json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}
