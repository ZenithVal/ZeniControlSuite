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
var configuration = builder.Configuration;


// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
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

builder.Services.AddSingleton<Service_Games>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_Games>());

builder.Services.AddSingleton<Service_Points>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_Points>());

builder.Services.AddSingleton<Service_BindingTrees>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_BindingTrees>());

builder.Services.AddSingleton<Service_Intiface>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_Intiface>());

builder.Services.AddScoped<AuthenticationStateProvider, DiscordAuthStateProvider>();

builder.Services.AddAuthentication(opt =>
    {
        opt.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        opt.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme = DiscordDefaults.AuthenticationScheme;
    })
    .AddCookie(opt =>
    {
        opt.Cookie.SameSite = SameSiteMode.Lax;
        opt.Cookie.SecurePolicy = CookieSecurePolicy.None;
        opt.Cookie.HttpOnly = true;
        opt.Cookie.Name = "ZeniControlSuite";
        opt.LoginPath = "/api/account/login";
        opt.LogoutPath = "/api/account/logout";
        opt.AccessDeniedPath = "/AccessDenied";
        opt.Cookie.IsEssential = true;
        opt.Events.OnValidatePrincipal = async context =>
        {
            var user = context.Principal;

            if (user.Identity?.IsAuthenticated == true)
            {
                var userID = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (userID != null && Whitelist.usersToAccept.ContainsKey(userID))
                {
                    var claims = DiscordAuthStateProvider.GetClaims(user, userID);

                    var identity = new ClaimsIdentity(claims, "Discord");
                    var principal = new ClaimsPrincipal(identity);

                    DiscordAuthStateProvider.AddUserToAcceptedList(userID);

                    context.ReplacePrincipal(principal);
                }
                else
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                    DiscordAuthStateProvider.AddUserToDeniedList(user);
                }
            }
        };
    })
    .AddDiscord(opt =>
    {
        string appId = string.Empty;
        string appSecret = string.Empty;
        string clientId = string.Empty;

        //This should really be in a try catch, right? I always fuck up my jsons
        var json = File.ReadAllText("Configs/Discord.json");
        var jObject = JsonConvert.DeserializeObject<dynamic>(json);

        appId = jObject.AppID;
        appSecret = jObject.AppSecret;
        clientId = jObject.ClientID;

        opt.AppId = appId;
        opt.AppSecret = appSecret;
        opt.ClientId = clientId ?? string.Empty;
        opt.Scope.Add("identify");
        opt.CallbackPath = new PathString("/signin-discord");

        //Required for accessing the oauth2 token in order to make requests on the user's behalf, ie. accessing the user's guild list
        opt.SaveTokens = true;

        opt.Events = new OAuthEvents {
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

builder.Services.AddAuthenticationCore();
builder.Services.AddScoped<AuthenticationStateProvider, DiscordReAuthStateProvider>();

Whitelist.loadDiscordUsersJson();

builder.Services.Configure<CookiePolicyOptions>(opt =>
{
    opt.MinimumSameSitePolicy = SameSiteMode.Lax;
    opt.HttpOnly = HttpOnlyPolicy.Always;
    opt.Secure = CookieSecurePolicy.None;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.Urls.Add("http://localhost:8080");

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

app.MapDefaultControllerRoute();
app.MapControllers();
app.UseAntiforgery();

app.Run();
