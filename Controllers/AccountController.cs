using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using ZeniControlSuite.Authentication;
using ZeniControlSuite.Services;

namespace ZeniControlSuite.Controllers;

[Route("api/[controller]/[action]")]
public class AccountController : ControllerBase
{
    private readonly Service_AccessCodes _accessCodes;
    private readonly Service_Logs _logs;
    private readonly Service_PageAccess _pageAccess;

    public IDataProtectionProvider Provider { get; }

    public AccountController(IDataProtectionProvider provider, Service_AccessCodes accessCodes, Service_Logs logs, Service_PageAccess pageAccess)
    {
        Provider = provider;
        _accessCodes = accessCodes;
        _logs = logs;
        _pageAccess = pageAccess;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string returnUrl = "/")
    {
        return LocalRedirect($"/Login?discordError=visitorCodeRequired&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public IActionResult DiscordLogin([FromForm] string visitorCode, [FromForm] string returnUrl = "/")
    {
        if (!DiscordAuthAvailability.Enabled)
        {
            return LocalRedirect($"/Login?discord=disabled&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        if (!_accessCodes.VerifyVisitorCode(visitorCode))
        {
            _logs.AddLog("Authentication", "Discord", "Discord login blocked: invalid visitor code", MudBlazor.Severity.Warning, MudBlazor.Variant.Outlined);
            return LocalRedirect($"/Login?discordError=invalidVisitorCode&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        _logs.AddLog("Authentication", "Discord", "Discord login started after visitor code check", MudBlazor.Severity.Info, MudBlazor.Variant.Outlined);
        return Challenge(new AuthenticationProperties { RedirectUri = LoginDestination(returnUrl) }, "Discord");
    }

    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AdminPasswordLogin([FromForm] string password, [FromForm] string returnUrl = "/")
    {
        if (!_accessCodes.VerifyAdminPassword(password))
        {
            _logs.AddLog("Authentication", "Admin Password", "Admin login failed", MudBlazor.Severity.Warning, MudBlazor.Variant.Outlined);
            return LocalRedirect($"/Login?adminError=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var principal = SuiteClaims.CreateAdminPasswordPrincipal();
        await SignIn(principal);

        _logs.AddLog("Authentication", principal.FindFirstValue(ClaimTypes.Name) ?? "Generated Admin", "Admin login succeeded", MudBlazor.Severity.Info, MudBlazor.Variant.Outlined);
        return LocalRedirect(LoginDestination(returnUrl));
    }

    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> VisitorCodeLogin([FromForm] string visitorCode, [FromForm] string returnUrl = "/")
    {
        if (!_accessCodes.VerifyVisitorCode(visitorCode))
        {
            _logs.AddLog("Authentication", "Visitor Code", "Visitor login failed", MudBlazor.Severity.Warning, MudBlazor.Variant.Outlined);
            return LocalRedirect($"/Login?visitorError=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var visitorNumber = SuiteClaims.NextVisitorNumber();
        var principal = SuiteClaims.CreateVisitorCodePrincipal(visitorNumber);
        await SignIn(principal);

        _logs.AddLog("Authentication", principal.FindFirstValue(ClaimTypes.Name) ?? $"Visitor {visitorNumber}", "Visitor login succeeded", MudBlazor.Severity.Info, MudBlazor.Variant.Outlined);
        return LocalRedirect(LoginDestination(returnUrl));
    }

    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public Task<IActionResult> PasswordLogin([FromForm] string password, [FromForm] string returnUrl = "/")
    {
        return AdminPasswordLogin(password, returnUrl);
    }

    [HttpGet]
    public async Task<IActionResult> LogOut(string returnUrl = "/")
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect(returnUrl);
    }

    private string LoginDestination(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || returnUrl == "/")
        {
            return _pageAccess.FirstAvailablePath();
        }

        return returnUrl;
    }

    private Task SignIn(ClaimsPrincipal principal)
    {
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = true,
            IssuedUtc = DateTimeOffset.UtcNow
        });
    }
}
