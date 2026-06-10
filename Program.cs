using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Discord.OAuth2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.CookiePolicy;
using MudBlazor;
using MudBlazor.Services;
using Newtonsoft.Json;
using ZeniControlSuite.Authentication;
using ZeniControlSuite.Components;
using ZeniControlSuite.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddControllers();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.HttpOnly = true;
});

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.ClearAfterNavigation = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Outlined;
});

builder.Services.AddSingleton<Service_Logs>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_Logs>());

builder.Services.AddSingleton<Service_AccessCodes>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_AccessCodes>());

builder.Services.AddSingleton<Service_PageAccess>();

builder.Services.AddSingleton<Service_Games>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_Games>());

builder.Services.AddSingleton<Service_Points>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_Points>());

builder.Services.AddSingleton<Service_BindingTrees>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_BindingTrees>());

builder.Services.AddSingleton<Service_OSC>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_OSC>());

builder.Services.AddSingleton<Service_Avatars>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_Avatars>());

builder.Services.AddSingleton<Service_Intiface>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_Intiface>());

builder.Services.AddScoped<AuthenticationStateProvider, SuiteAuthenticationStateProvider>();

var authenticationBuilder = builder.Services.AddAuthentication(opt =>
{
    opt.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    opt.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(opt =>
{
    opt.Cookie.SameSite = SameSiteMode.Lax;
    opt.Cookie.SecurePolicy = CookieSecurePolicy.None;
    opt.Cookie.HttpOnly = true;
    opt.Cookie.Name = "ZeniControlSuite";
    opt.LoginPath = "/Login";
    opt.LogoutPath = "/api/account/logout";
    opt.AccessDeniedPath = "/AccessDenied";
    opt.Cookie.IsEssential = true;
    opt.Events.OnValidatePrincipal = async context =>
    {
        var user = context.Principal;

        if (user?.Identity?.IsAuthenticated != true)
        {
            context.RejectPrincipal();
            return;
        }

        if (SuiteClaims.IsLocalSuiteUser(user))
        {
            return;
        }

        var userID = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(userID) && Whitelist.usersToAccept.ContainsKey(userID))
        {
            var claims = DiscordAuthStateProvider.GetClaims(user, userID);
            var identity = new ClaimsIdentity(claims, SuiteClaims.AuthenticationTypeDiscord);
            var principal = new ClaimsPrincipal(identity);

            DiscordAuthStateProvider.AddUserToAcceptedList(userID);
            context.ReplacePrincipal(principal);
            context.ShouldRenew = true;
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync();
        DiscordAuthStateProvider.AddUserToDeniedList(user);
    };
});

if (TryLoadDiscordSettings(out var discordSettings))
{
    DiscordAuthAvailability.Enabled = true;
    authenticationBuilder.AddDiscord(opt =>
    {
        opt.AppId = discordSettings.AppId;
        opt.AppSecret = discordSettings.AppSecret;
        opt.ClientId = discordSettings.ClientId;
        opt.Scope.Add("identify");
        opt.CallbackPath = new PathString("/signin-discord");
        opt.SaveTokens = true;

        opt.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, opt.UserInformationEndpoint);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                var response = await opt.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();

                var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                context.RunClaimActions(user.RootElement);
            },
        };

        opt.AccessDeniedPath = "/";
    });
}

builder.Services.AddAuthenticationCore();

Whitelist.loadDiscordUsersJson();

builder.Services.Configure<CookiePolicyOptions>(opt =>
{
    opt.MinimumSameSitePolicy = SameSiteMode.Lax;
    opt.HttpOnly = HttpOnlyPolicy.Always;
    opt.Secure = CookieSecurePolicy.None;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.Urls.Add("http://localhost:8080");

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultControllerRoute();
app.MapControllers();
app.UseAntiforgery();

app.Run();

static bool TryLoadDiscordSettings(out DiscordSettings settings)
{
    settings = new DiscordSettings();

    try
    {
        if (!File.Exists("Configs/Discord.json"))
        {
            Console.WriteLine("Discord auth optional: Configs/Discord.json was not found.");
            return false;
        }

        var json = File.ReadAllText("Configs/Discord.json");
        var jObject = JsonConvert.DeserializeObject<dynamic>(json);

        settings = new DiscordSettings
        {
            AppId = Convert.ToString(jObject?.AppID) ?? string.Empty,
            AppSecret = Convert.ToString(jObject?.AppSecret) ?? string.Empty,
            ClientId = Convert.ToString(jObject?.ClientID) ?? string.Empty
        };

        var configured = !string.IsNullOrWhiteSpace(settings.AppId)
            && !string.IsNullOrWhiteSpace(settings.AppSecret)
            && !string.IsNullOrWhiteSpace(settings.ClientId);

        Console.WriteLine(configured ? "Discord auth enabled." : "Discord auth optional: Discord.json is incomplete.");
        return configured;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Discord auth optional: failed to read Discord.json: {ex.Message}");
        return false;
    }
}

sealed class DiscordSettings
{
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
}
