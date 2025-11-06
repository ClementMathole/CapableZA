using Capableza.Web.ViewModels; 
using Capableza.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System; 
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CapablezaWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly AuthService _authService;
        private readonly FirestoreService _firestoreService;

        public AccountController(AuthService authService, FirestoreService firestoreService)
        {
            _authService = authService;
            _firestoreService = firestoreService;
        }

        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {

            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("admin")) return RedirectToAction("Index", "Admin");
                if (User.IsInRole("employee")) return RedirectToAction("Dashboard", "Employee");
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                try
                {
                    var firebaseAuthResponse = await _authService.LoginAsync(model.Email, model.Password);

                    if (firebaseAuthResponse != null && !string.IsNullOrEmpty(firebaseAuthResponse.LocalId))
                    {
                        var userRoleInfo = await _authService.GetUserRoleAsync(firebaseAuthResponse.LocalId);

                        string? role = userRoleInfo?.Role?.ToLower();

                        if (string.IsNullOrEmpty(role))
                        {
                            ModelState.AddModelError(string.Empty, "Login failed: User role not configured correctly.");
                            await _firestoreService.LogActionAsync(firebaseAuthResponse.LocalId, "webLoginFail", "Role document missing/invalid");
                            // await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); signs out partially logged in users
                            return View(model);
                        }

                        if (role != "admin" && role != "employee")
                        {
                            ModelState.AddModelError(string.Empty, "Access denied: Invalid user role.");
                            await _firestoreService.LogActionAsync(firebaseAuthResponse.LocalId, "webLoginFail", $"Invalid role: {role}");
                            return View(model);
                        }

                        var claims = new List<Claim> {
                            new Claim(ClaimTypes.NameIdentifier, firebaseAuthResponse.LocalId),
                            new Claim(ClaimTypes.Email, firebaseAuthResponse.Email),
                            new Claim(ClaimTypes.Role, role)
                        };
                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                        var authProperties = new AuthenticationProperties
                        {
                            IsPersistent = model.RememberMe,
                            ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
                        };

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties);

                        await _firestoreService.LogActionAsync(firebaseAuthResponse.LocalId, "webLoginSuccess", $"Role: {role}");

                        if (Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }
                        else
                        {
                            return RedirectToAction(role == "admin" ? "Index" : "Dashboard",
                                                    role == "admin" ? "Admin" : "Employee");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                        return View(model);
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                    return View(model);
                }
            }
            return View(model);
        }

        #region Other IActionsResults
        [AllowAnonymous]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken] 
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model) {
            if (ModelState.IsValid) {
                try {
                    await _authService.SendPasswordResetEmailAsync(model.Email);
                    ViewBag.Message = "Reset link sent.";
                    return View("ForgotPasswordConfirmation");
                } catch (Exception ex) {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }
            return View(model);
        }

        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation() => View();
        [HttpPost]
        [ValidateAntiForgeryToken] 
        public async Task<IActionResult> Logout() {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied() => View();
        #endregion Other IActionsResults

    }
}