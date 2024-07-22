using Discord.OAuth2;
using Microsoft.AspNetCore.Authentication.Cookies;
using MudBlazor;
using MudBlazor.Services;
using ZeniControlSuite.Components;
using ZeniControlSuite.Services;
using Newtonsoft.Json;
using ZeniControlSuite.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

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
        opt.AccessDeniedPath = "/"; //Send the user to home page instead of access denied
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
            OnRemoteFailure = context =>
            {
                context.Response.Redirect("error?message=" + context.Failure.Message);
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
        
        opt.AccessDeniedPath = "/";
    });

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
    .AddInteractiveServerRenderMode();
app.MapBlazorHub(path:"/app");
app.MapDefaultControllerRoute();
app.UseAntiforgery();

app.Run();
