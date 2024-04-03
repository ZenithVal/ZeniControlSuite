using MudBlazor;
using MudBlazor.Services;
using ZeniControlSuite.Components;

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

builder.Services.AddSingleton<LogService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LogService>());

builder.Services.AddSingleton<GamesService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GamesService>());

builder.Services.AddSingleton<PointsService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PointsService>());

builder.Services.AddSingleton<BindingTreesService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<BindingTreesService>());


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
