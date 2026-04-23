using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CRMBlazorServerRBS.Models;
using System.Security.Principal;

namespace CRMBlazorServerRBS.Controllers
{
    [Route("Account/[action]")]
    public partial class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly RoleManager<ApplicationRole> roleManager;
        private readonly IWebHostEnvironment env;
        private readonly IConfiguration configuration;
        private readonly SecurityService _securityService;

        public AccountController(IWebHostEnvironment env, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager, IConfiguration configuration, SecurityService securityService)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.env = env;
            this.configuration = configuration;
            this._securityService = securityService;
        }

        private IActionResult RedirectWithError(string error, string redirectUrl = null)
        {
            var encodedError = Uri.EscapeDataString(error ?? "");
            if (!string.IsNullOrEmpty(redirectUrl))
            {
                return Redirect($"~/Login?error={encodedError}&redirectUrl={Uri.EscapeDataString(redirectUrl)}");
            }
            else
            {
                return Redirect($"~/Login?error={encodedError}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl)
        {
            if (returnUrl != "/" && !string.IsNullOrEmpty(returnUrl))
            {
                return Redirect($"~/Login?redirectUrl={Uri.EscapeDataString(returnUrl)}");
            }

            return Redirect("~/Login");
        }

        [HttpPost]
        public async Task<IActionResult> Login(string userName, string password, string redirectUrl)
        {
            if (env.EnvironmentName == "Development" && userName == "admin" && password == "admin")
            {
                var claims = new List<Claim>()
                {
                    new Claim(ClaimTypes.Name, "admin"),
                    new Claim(ClaimTypes.Email, "admin")
                };

                roleManager.Roles.ToList().ForEach(r => claims.Add(new Claim(ClaimTypes.Role, r.Name)));
                await signInManager.SignInWithClaimsAsync(new ApplicationUser { UserName = userName, Email = userName }, isPersistent: false, claims);

                return Redirect($"~/{redirectUrl}");
            }

            if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password))
            {
                var result = await signInManager.PasswordSignInAsync(userName, password, false, false);

                if (result.Succeeded)
                {
                    return Redirect($"~/{redirectUrl}");
                }
            }

            return RedirectWithError("Invalid user or password", redirectUrl);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword)
        {
            if (string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword))
            {
                return BadRequest("Invalid password");
            }

            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var user = await userManager.FindByIdAsync(id);

            var result = await userManager.ChangePasswordAsync(user, oldPassword, newPassword);

            if (result.Succeeded)
            {
                return Ok();
            }

            var message = string.Join(", ", result.Errors.Select(error => error.Description));

            return BadRequest(message);
        }

        [HttpPost]
        public ApplicationAuthenticationState CurrentUser()
        {
            return new ApplicationAuthenticationState
            {
                IsAuthenticated = User.Identity.IsAuthenticated,
                Name = User.Identity.Name,
                Claims = User.Claims.Select(c => new ApplicationClaim { Type = c.Type, Value = c.Value })
            };
        }

        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();

            return Redirect("~/");
        }

        /// <summary>
        /// Windows-аутентификация (Negotiate/Kerberos/NTLM).
        /// Атрибут [Authorize(AuthenticationSchemes = "Negotiate")] автоматически
        /// вызывает браузерный диалог Windows, если пользователь ещё не аутентифицирован.
        /// После успешной Windows-аутентификации находим соответствующего Identity-пользователя
        /// и выдаём стандартный Identity cookie.
        /// </summary>
        [HttpGet]
        [Authorize(AuthenticationSchemes = "Negotiate")]
        public async Task<IActionResult> WindowsLogin(string redirectUrl)
        {
            var windowsUsername = User.Identity?.Name;
            var shortName = windowsUsername?.Contains('\\') == true
                ? windowsUsername.Split('\\')[1]
                : windowsUsername;

            // Get Windows SID — survives username/email renames in AD
            var windowsIdentity = User.Identity as System.Security.Principal.WindowsIdentity;
            var currentSid = windowsIdentity?.User?.Value;

            // 1. Find by SID first (most reliable)
            ApplicationUser user = null;
            if (!string.IsNullOrEmpty(currentSid))
                user = await _securityService.GetUserBySidAsync(currentSid);

            // 2. Fallback: find by Windows username
            if (user == null)
                user = await userManager.FindByNameAsync(windowsUsername)
                    ?? await userManager.FindByNameAsync(shortName);

            if (user == null)
                return RedirectWithError($"Windows-пользователь '{shortName}' не найден в системе.", redirectUrl);

            // 3. Sync current AD data → update DB if anything changed
            var adInfo = _securityService.GetAdInfoBySamAccountName(shortName);
            bool needsUpdate = false;

            if (adInfo != null)
            {
                if (!string.IsNullOrEmpty(adInfo.Email) && user.Email != adInfo.Email)
                {
                    user.Email = adInfo.Email;
                    user.NormalizedEmail = adInfo.Email.ToUpperInvariant();
                    needsUpdate = true;
                }
                if (!string.IsNullOrEmpty(adInfo.FirstName) && user.FirstName != adInfo.FirstName)
                { user.FirstName = adInfo.FirstName; needsUpdate = true; }

                if (!string.IsNullOrEmpty(adInfo.LastName) && user.LastName != adInfo.LastName)
                { user.LastName = adInfo.LastName; needsUpdate = true; }
            }

            // Save SID if not yet stored
            if (!string.IsNullOrEmpty(currentSid) && user.Sid != currentSid)
            { user.Sid = currentSid; needsUpdate = true; }

            if (needsUpdate)
                await userManager.UpdateAsync(user);

            await signInManager.SignInAsync(user, isPersistent: false);
            return Redirect($"~/{redirectUrl}");
        }
    }
}