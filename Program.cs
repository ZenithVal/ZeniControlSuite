using MudBlazor;
using MudBlazor.Services;
using ZeniControlSuite.Components;
using Newtonsoft.Json;

using Discord.OAuth2;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

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

builder.Services.AddAuthentication(opt =>
{
    opt.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    opt.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = DiscordDefaults.AuthenticationScheme;
})
            .AddCookie()
            .AddDiscord(x =>
            {
                string DiscordConfig = File.ReadAllText("Configs/Discord.json");
                dynamic DiscordConfigJson = JsonConvert.DeserializeObject(DiscordConfig);

                x.AppId = DiscordConfigJson.AppId;
                x.AppSecret = DiscordConfigJson.AppSecret;
                x.Scope.Add("guilds");

                x.SaveTokens = true;
            });

builder.Services.AddSingleton<Service_Logs>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_Logs>());

builder.Services.AddSingleton<Service_Games>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_Games>());

builder.Services.AddSingleton<Service_Points>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_Points>());

builder.Services.AddSingleton<Service_BindingTrees>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Service_BindingTrees>());


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
