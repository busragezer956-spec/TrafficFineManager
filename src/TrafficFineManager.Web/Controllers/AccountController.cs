using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TrafficFineManager.Web.ViewModels;
namespace TrafficFineManager.Web.Controllers;
public class AccountController(SignInManager<IdentityUser> signIn) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl) => View(new LoginVm { ReturnUrl = returnUrl });
    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginVm input)
    {
        if (!ModelState.IsValid) return View(input);
        var result = await signIn.PasswordSignInAsync(input.Email.Trim(), input.Password, false, lockoutOnFailure: true);
        if (result.Succeeded) return Url.IsLocalUrl(input.ReturnUrl) ? LocalRedirect(input.ReturnUrl!) : RedirectToAction("Index", "Dashboard");
        ModelState.AddModelError("", result.IsLockedOut ? "Çok fazla hatalı giriş. 5 dakika sonra tekrar deneyin." : "E-posta veya şifre hatalı.");
        return View(input);
    }
    [Authorize, HttpPost]
    public async Task<IActionResult> Logout() { await signIn.SignOutAsync(); return RedirectToAction(nameof(Login)); }
    [AllowAnonymous, HttpGet]
    public IActionResult Denied() { Response.StatusCode = 403; return View(); }
}
