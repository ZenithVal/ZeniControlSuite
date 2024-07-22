using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ZeniControlSuite.Services;
using MudBlazor;
using Microsoft.AspNetCore.Components;

namespace ZeniControlSuite.Data;

public class Service_Users
{
    private static HttpClient client = new HttpClient();
	[Inject] private Service_Logs LogService { get; set; } = default!;


    /// Parses the user's discord claim for their `identify` information
    public DiscordUserClaim GetInfo(HttpContext httpContext)
    {
        if (!httpContext.User.Identity.IsAuthenticated)
        {
            return null;
        }
    
        var claims = httpContext.User.Claims;
        bool? verified;
        if (bool.TryParse(claims.FirstOrDefault(x => x.Type == "urn:discord:verified")?.Value, out var _verified))
        {
            verified = _verified;
        }
        else
        {
            verified = null;
        }

        var userClaim = new DiscordUserClaim {
            UserId = ulong.Parse(claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value),
            Name = claims.First(x => x.Type == "urn:discord:global_name").Value,
            Avatar = claims.First(x => x.Type == "urn:discord:avatar").Value,
            Email = claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value,
            Verified = verified,
        };

        LogService.AddLog("User", "System", $"User {userClaim.Name} logged in", Severity.Info, Variant.Outlined);
    
        return userClaim;
    }
    
    /// Gets the user's discord oauth2 access token
    public async Task<string> GetTokenAsync(HttpContext httpContext)
    {
        if (!httpContext.User.Identity.IsAuthenticated)
        {
            return null;
        }
    
        var tk = await httpContext.GetTokenAsync("Discord", "access_token");
        return tk;
    }
    
    /// Gets a list of the user's guilds, Requires `Guilds` scope
    public async Task<List<Guild>> GetUserGuildsAsync(HttpContext httpContext)
    {
        if (!httpContext.User.Identity.IsAuthenticated)
        {
            return null;
        }
    
        var token = await GetTokenAsync(httpContext);
    
        var guildEndpoint = Discord.OAuth2.DiscordDefaults.UserInformationEndpoint + "/guilds";
    
        using (var request = new HttpRequestMessage(HttpMethod.Get, guildEndpoint))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    
            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
    
            var content = await response.Content.ReadAsStringAsync();
            try
            {
                var guilds = Guild.ListFromJson(content);
                return guilds;
            }
            catch
            {
                return null;
            }
        }
    }
    
    public class DiscordUserClaim
    {
        public ulong UserId { get; set; }
        public string Name { get; set; }
        public string Avatar { get; set; }
    
        /// Will be null if the email scope is not provided
        public string Email { get; set; } = null;
    
        /// Whether the email on this account has been verified, can be null
        public bool? Verified { get; set; } = null;

        public List<string> Roles { get; set; }
}

    public class DiscordUser
    {
        public ulong Id { get; set; }
        public string DisplayName { get; set; }
        public List<string> Roles { get; set; }
    }

    public class Guild
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    
        [JsonProperty("name")]
        public string Name { get; set; }
    
        [JsonProperty("icon")]
        public string Icon { get; set; }
    
        [JsonProperty("owner")]
        public bool Owner { get; set; }
    
        [JsonProperty("permissions")]
        public long Permissions { get; set; }
    
        [JsonProperty("features")]
        public List<string> Features { get; set; }
    
        public static List<Guild> ListFromJson(string json) => JsonConvert.DeserializeObject<List<Guild>>(json, Settings);
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
	}

}  
