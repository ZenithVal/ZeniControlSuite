using Discord.OAuth2;
using Microsoft.AspNetCore.Authentication.Cookies;
using MudBlazor;
using MudBlazor.Services;
using ZeniControlSuite.Components;
using ZeniControlSuite.Services;
using Microsoft.Extensions.Configuration;
using ZeniControlSuite.Data;
using Newtonsoft.Json;
using ZeniControlSuite.Components.Pages;
using ZeniControlSuite;


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

builder.Services.AddSingleton<Service_User>();

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
builder.Services.AddAuthentication(opt =>
    {
        opt.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        opt.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme = DiscordDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddDiscord(x =>
    {
        string appId = string.Empty;
        string appSecret = string.Empty;
        string clientId = string.Empty;

        try
        {
            var json = File.ReadAllText("Configs/Discord.json");
            var jObject = JsonConvert.DeserializeObject<dynamic>(json);
            appId = jObject.AppID;
            appSecret = jObject.AppSecret;
            clientId = jObject.ClientID;
            //whitelist = jObject.Whitelist.ToObject<List<string>>();

/*            Console.WriteLine("Whitelist:");
            foreach (var item in whitelist)
            {
				Console.WriteLine(item);
			}*/
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error loading Discord.json:\n{e.Message}");
            Console.ReadKey();
            Environment.Exit(0);
        }

        x.AppId = appId;
        x.AppSecret = appSecret;
        x.ClientId = clientId ?? string.Empty;
        x.Scope.Add("identify");

        //Required for accessing the oauth2 token in order to make requests on the user's behalf, ie. accessing the user's guild list
        x.SaveTokens = true;
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
