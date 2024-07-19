using Discord.OAuth2;
using Microsoft.AspNetCore.Authentication.Cookies;
using MudBlazor;
using MudBlazor.Services;
using ZeniControlSuite.Components;
using ZeniControlSuite.Services;
using Microsoft.Extensions.Configuration;
using ZeniControlSuite.Data;


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

builder.Services.AddSingleton<UserService>();

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
        x.AppId = configuration["Discord:AppId"];
        x.AppSecret = configuration["Discord:AppSecret"];
        x.ClientId = configuration["Discord:ClientId"] ?? string.Empty;
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
app.MapBlazorHub("/app");
app.MapDefaultControllerRoute();
app.UseAntiforgery();

app.Run();
