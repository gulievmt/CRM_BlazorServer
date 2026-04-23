using System;
using System.Collections.Generic;
using System.Data;
using System.DirectoryServices;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CRMBlazorServerRBS.Models;
using Dapper;

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
        private readonly IDbConnection _db;

        public AccountController(IWebHostEnvironment env, SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager,
            IConfiguration configuration, IDbConnection db)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.env = env;
            this.configuration = configuration;
            this._db = db;
        }

        // ── AD helpers (no dependency on Blazor NavigationManager) ─────────

        private async Task<ApplicationUser> FindUserBySidAsync(string sid)
        {
            if (string.IsNullOrEmpty(sid)) return null;
            return await _db.QueryFirstOrDefaultAsync<ApplicationUser>(
                "SELECT * FROM [dbo].[AspNetUsers] WHERE [Sid] = @Sid", new { Sid = sid });
        }

        private static (string Email, string FirstName, string LastName, string Sid) GetAdInfo(string samAccountName)
        {
            if (string.IsNullOrWhiteSpace(samAccountName))
                return default;
            try
            {
                using var entry = new DirectoryEntry("LDAP://fincaint.local");
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectClass=user)(objectCategory=person)(sAMAccountName={samAccountName}))",
                    SizeLimit = 1
                };
                searcher.PropertiesToLoad.AddRange(
                    new[] { "givenName", "sn", "mail", "objectSid" });
                var r = searcher.FindOne();
                if (r == null) return default;

                string sid = "";
                if (r.Properties["objectSid"]?.Count > 0 && r.Properties["objectSid"][0] is byte[] sidBytes)
                    sid = new SecurityIdentifier(sidBytes, 0).Value;

                static string P(SearchResult sr, string k) =>
                    sr.Properties[k]?.Count > 0 ? sr.Properties[k][0]?.ToString() ?? "" : "";

                return (P(r, "mail"), P(r, "givenName"), P(r, "sn"), sid);
            }
            catch { return default; }
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

            // Get Windows SID — unique identifier that survives username/email renames
            var windowsIdentity = User.Identity as WindowsIdentity;
            var currentSid = windowsIdentity?.User?.Value;

            // 1. Find by SID (most reliable)
            ApplicationUser user = null;
            if (!string.IsNullOrEmpty(currentSid))
                user = await FindUserBySidAsync(currentSid);

            // 2. Fallback: query AD for the SID by username, search DB by that SID.
            //    Handles existing users whose [Sid] column was not yet populated.
            if (user == null && !string.IsNullOrEmpty(shortName))
            {
                var (_, _, _, adSidFallback) = GetAdInfo(shortName);
                if (!string.IsNullOrEmpty(adSidFallback))
                    user = await FindUserBySidAsync(adSidFallback);
            }

            // 3. Last resort: find by username string (for dev/local accounts)
            if (user == null)
                user = await _db.QueryFirstOrDefaultAsync<ApplicationUser>(
                    "SELECT * FROM [dbo].[AspNetUsers] WHERE [UserName] = @UserName OR [NormalizedUserName] = @Normalized",
                    new { UserName = shortName, Normalized = shortName?.ToUpperInvariant() });

            if (user == null)
                return RedirectWithError($"Windows-пользователь '{shortName}' не найден в системе.", redirectUrl);

            // 3. Sync AD data → update DB if Email / FirstName / LastName / Sid changed.
            // Use Dapper directly to avoid EF tracking conflicts: the user object may have
            // been loaded via Dapper (untracked), so passing it to userManager.UpdateAsync
            // would cause "another instance with the same key is already being tracked".
            var (adEmail, adFirstName, adLastName, adSid) = GetAdInfo(shortName);

            var newUserName  = !string.IsNullOrEmpty(shortName)   ? shortName   : user.UserName;
            var newEmail     = !string.IsNullOrEmpty(adEmail)     ? adEmail     : user.Email;
            var newFirstName = !string.IsNullOrEmpty(adFirstName) ? adFirstName : user.FirstName;
            var newLastName  = !string.IsNullOrEmpty(adLastName)  ? adLastName  : user.LastName;
            var newSid       = !string.IsNullOrEmpty(currentSid)  ? currentSid  : user.Sid;

            bool needsUpdate =
                newUserName  != user.UserName  ||
                newEmail     != user.Email     ||
                newFirstName != user.FirstName ||
                newLastName  != user.LastName  ||
                newSid       != user.Sid;

            if (needsUpdate)
            {
                await _db.ExecuteAsync(@"
                    UPDATE [dbo].[AspNetUsers]
                    SET    UserName           = @UserName,
                           NormalizedUserName = @NormalizedUserName,
                           Email              = @Email,
                           NormalizedEmail    = @NormalizedEmail,
                           FirstName          = @FirstName,
                           LastName           = @LastName,
                           Sid                = @Sid
                    WHERE  Id = @Id",
                    new {
                        UserName           = newUserName,
                        NormalizedUserName = newUserName?.ToUpperInvariant(),
                        Email              = newEmail,
                        NormalizedEmail    = newEmail?.ToUpperInvariant(),
                        FirstName          = newFirstName,
                        LastName           = newLastName,
                        Sid                = newSid,
                        user.Id
                    });
            }

            await signInManager.SignInAsync(user, isPersistent: false);
            return Redirect($"~/{redirectUrl}");
        }
    }
}