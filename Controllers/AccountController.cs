using AlquilerVestimentasLucy.Models;
using AlquilerVestimentasLucy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AlquilerVestimentasLucy.Controllers;

/// <summary>
/// Autenticación, cierre de sesión y denegación de acceso (RF-01).
/// </summary>
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var usuario = await _userManager.FindByEmailAsync(model.Usuario)
                      ?? await _userManager.FindByNameAsync(model.Usuario);

        if (usuario is null || !usuario.Activo)
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
            return View(model);
        }

        var resultado = await _signInManager.PasswordSignInAsync(
            usuario,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (resultado.Succeeded)
        {
            _logger.LogInformation("Inicio de sesión correcto para {Usuario}.", usuario.UserName);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        if (resultado.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "La cuenta está bloqueada temporalmente por demasiados intentos fallidos.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
        }

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
