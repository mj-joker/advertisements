using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Avatisment.Models;
using Avatisment.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Avatisment.Controllers;

public class AccountController : Controller
{
    private readonly IAvatismentDataStore _store;

    public AccountController(IAvatismentDataStore store)
    {
        _store = store;
    }

    private string? CurrentUserId =>
        User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    // ================= Login =================

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (_store.IsLockedOut(model.Email, out var remaining))
        {
            model.Error = $"Too many failed attempts. Try again in {Math.Ceiling(remaining!.Value.TotalMinutes)} minute(s).";
            return View(model);
        }

        var user = _store.GetUserByEmail(model.Email);
        if (user is null || !VerifyPassword(model.Password, user.PasswordHash))
        {
            _store.RegisterFailedLogin(model.Email);
            model.Error = "That email and password don't match any account.";
            return View(model);
        }

        _store.ClearFailedLogins(model.Email);
        await SignInAsync(user);

        if (!user.EmailVerified) return RedirectToAction("VerifyEmail");
        if (!user.PhoneVerified) return RedirectToAction("VerifyPhone");

        return !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? Redirect(model.ReturnUrl)
            : RedirectToAction("Index", "Home");
    }

    // ================= Register =================

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (_store.GetUserByEmail(model.Email) is not null)
        {
            model.Error = "An account with that email already exists.";
            return View(model);
        }

        if (_store.GetUserByPhone(model.PhoneNumber) is not null)
        {
            model.Error = "An account with that phone number already exists.";
            return View(model);
        }

        var handle = "@" + model.DisplayName.ToLower().Replace(" ", ".").Trim('.');
        var user = _store.CreateUser(model.DisplayName, handle, model.Email, model.PhoneNumber, HashPassword(model.Password));

        _store.GenerateEmailCode(user.Id);
        await SignInAsync(user);
        return RedirectToAction("VerifyEmail");
    }

    // ================= Email verification =================

    [HttpGet, Authorize]
    public IActionResult VerifyEmail()
    {
        var user = _store.GetUserById(CurrentUserId!);
        if (user is null) return RedirectToAction("Login");
        if (user.EmailVerified)
            return user.PhoneVerified
                ? RedirectToAction("Index", "Home")
                : RedirectToAction("VerifyPhone");

        var code = user.EmailVerificationCode ?? _store.GenerateEmailCode(user.Id);
        return View(new VerifyCodeViewModel { Destination = Mask(user.Email), DemoCode = code });
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public IActionResult VerifyEmail(VerifyCodeViewModel model)
    {
        var user = _store.GetUserById(CurrentUserId!);
        if (user is null) return RedirectToAction("Login");

        if (ModelState.IsValid && _store.VerifyEmailCode(user.Id, model.Code))
        {
            return user.PhoneVerified
                ? RedirectToAction("Index", "Home")
                : RedirectToAction("VerifyPhone");
        }

        model.Error = "That code is incorrect or has expired.";
        model.Destination = Mask(user.Email);
        model.DemoCode = user.EmailVerificationCode;
        return View(model);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public IActionResult ResendEmailCode()
    {
        var uid = CurrentUserId;
        if (uid != null) _store.GenerateEmailCode(uid);
        return RedirectToAction("VerifyEmail");
    }

    // ================= Phone verification =================

    [HttpGet, Authorize]
    public IActionResult VerifyPhone()
    {
        var user = _store.GetUserById(CurrentUserId!);
        if (user is null) return RedirectToAction("Login");
        if (!user.EmailVerified) return RedirectToAction("VerifyEmail");
        if (user.PhoneVerified) return RedirectToAction("Index", "Home");

        var code = user.PhoneVerificationCode ?? _store.GeneratePhoneCode(user.Id);
        return View(new VerifyCodeViewModel { Destination = Mask(user.PhoneNumber), DemoCode = code });
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public IActionResult VerifyPhone(VerifyCodeViewModel model)
    {
        var user = _store.GetUserById(CurrentUserId!);
        if (user is null) return RedirectToAction("Login");

        if (ModelState.IsValid && _store.VerifyPhoneCode(user.Id, model.Code))
        {
            return RedirectToAction("Index", "Home");
        }

        model.Error = "That code is incorrect or has expired.";
        model.Destination = Mask(user.PhoneNumber);
        model.DemoCode = user.PhoneVerificationCode;
        return View(model);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public IActionResult ResendPhoneCode()
    {
        var uid = CurrentUserId;
        if (uid != null) _store.GeneratePhoneCode(uid);
        return RedirectToAction("VerifyPhone");
    }

    // ================= Logout =================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("AvatismentAuth");
        return RedirectToAction("Login");
    }

    // ---------- helpers ----------

    private async Task SignInAsync(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.DisplayName),
        };
        var identity = new ClaimsIdentity(claims, "AvatismentAuth");
        await HttpContext.SignInAsync("AvatismentAuth", new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        // Demo accounts (seed data) use the literal password "demo1234".
        if (hash == "seed") return password == "demo1234";
        return HashPassword(password) == hash;
    }

    private static string Mask(string value)
    {
        if (value.Contains('@'))
        {
            var parts = value.Split('@');
            var name = parts[0];
            var visible = name.Length <= 2 ? name : name[..2];
            return $"{visible}{new string('*', Math.Max(name.Length - 2, 1))}@{parts[1]}";
        }

        return value.Length <= 4 ? value : $"{new string('*', value.Length - 4)}{value[^4..]}";
    }
}
