using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace ZeniControlSuite.Services;

[Route("[controller]/[action]")]
public class AccountController : ControllerBase
{
    public IDataProtectionProvider Provider { get; }

    public AccountController(IDataProtectionProvider provider)
    {
        Provider = provider;
    }

    [HttpGet("login")]
    public IActionResult Login(string returnUrl = "/")
    {
        return Challenge(new AuthenticationProperties { RedirectUri = returnUrl }, "Discord");
    }

    [HttpGet("logout")]
    public async Task<IActionResult> LogOut(string returnUrl = "/")
    {
        //This removes the cookie assigned to the user login.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect(returnUrl);
    }
}