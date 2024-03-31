using Microsoft.AspNetCore;
using ZeniControlSuite.Components;

using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

builder.Services.AddSingleton<GamesPointsService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GamesPointsService>());

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
