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

    public static Dictionary<string, DiscordUser> acceptedMembers = new Dictionary<string, DiscordUser>();

    public static Dictionary<string, DiscordUser> deniedMembers = new Dictionary<string, DiscordUser>();

    public static void loadDiscordUsersJson()
    {
        try
        {
            var json = File.ReadAllText("Configs/DiscordUsers.json");
            var users = JsonConvert.DeserializeObject<Dictionary<string, DiscordUser>>(json);
            foreach (var user in users)
            {
                acceptedMembers.Add(user.Key, user.Value);
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
            var json = JsonConvert.SerializeObject(acceptedMembers, Formatting.Indented);
            File.WriteAllText("Configs/DiscordUsers.json", json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}
