// SQL MIGRATION REQUIRED before running the app:
// ALTER TABLE [dbo].[AspNetUsers] ADD [IsWindowsUser] BIT NOT NULL DEFAULT 0;
// ALTER TABLE [dbo].[AspNetUsers] ADD [Sid] NVARCHAR(200) NULL;
using CRMBlazorServerRBS.Data;
using CRMBlazorServerRBS.Models;
using Dapper;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.DirectoryServices;
using System.Security.Principal;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace CRMBlazorServerRBS
{
    public partial class SecurityService
    {

        private readonly HttpClient httpClient;

        private readonly Uri baseUri;

        private readonly ApplicationIdentityDbContext securitDbContext;

        private readonly NavigationManager navigationManager;

        public ApplicationUser User { get; private set; } = new ApplicationUser { Name = "Anonymous" };

        public ClaimsPrincipal Principal { get; private set; }

        private readonly string connectionString;
        private readonly IDbConnection _connection;
        private readonly UserManager<ApplicationUser> _userManager;


        public SecurityService(ApplicationIdentityDbContext securitContext, NavigationManager navigationManager,
                               IHttpClientFactory factory, IDbConnection connection,
                               UserManager<ApplicationUser> userManager)
        {
            this.securitDbContext = securitContext;
            this.baseUri = new Uri($"{navigationManager.BaseUri}api/Identity/");
            this.httpClient = factory.CreateClient("CRMBlazorServerRBS");
            this.navigationManager = navigationManager;
            this._connection = connection;
            this._connection.Open();
            this._userManager = userManager;
        }

        // Use a short-lived connection in GetRoles
        public async Task<IEnumerable<ApplicationRole>> GetRoles()
        {
            var roles = await _connection.QueryAsync<ApplicationRole>("SELECT Id, Name FROM [RadzenCRM].[dbo].[AspNetRoles]");
            return roles.ToList();
        }


        public bool IsInRole(params string[] roles)
        {
#if DEBUG
            if (User.Name == "admin")
            {
                return true;
            }
#endif

            if (roles.Contains("Everybody"))
            {
                return true;
            }

            if (!IsAuthenticated())
            {
                return false;
            }

            if (roles.Contains("Authenticated"))
            {
                return true;
            }

            return roles.Any(role => Principal.IsInRole(role));
        }

        public bool IsAuthenticated()
        {
            return Principal?.Identity.IsAuthenticated == true;
        }

        public async Task<bool> InitializeAsync(AuthenticationState result)
        {
            Principal = result.User;
#if DEBUG
            if (Principal.Identity.Name == "admin")
            {
                User = new ApplicationUser { Name = "Admin" };

                return true;
            }
#endif
            var userId = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId != null && User?.Id != userId)
            {
                User = await GetUserById(userId);
            }

            return IsAuthenticated();
        }


        public async Task<ApplicationAuthenticationState> GetAuthenticationStateAsync()
        {
            var uri =  new Uri($"{navigationManager.BaseUri}Account/CurrentUser");

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, uri));

            return await response.ReadAsync<ApplicationAuthenticationState>();
        }

        public void Logout()
        {
            navigationManager.NavigateTo("Account/Logout", true);
        }

        public void Login()
        {
            navigationManager.NavigateTo("Login", true);
        }

       

   
        public async Task<ApplicationRole> CreateRole(ApplicationRole role)
        {
            var uri = new Uri(baseUri, $"ApplicationRoles");

            var content = new StringContent(JsonSerializer.Serialize(role), Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(uri, content);

            return await response.ReadAsync<ApplicationRole>();
        }

        public async Task<HttpResponseMessage> DeleteRole(string id)
        {
            var uri = new Uri(baseUri, $"ApplicationRoles/{id}");

            return await httpClient.DeleteAsync(uri);
        }

        public async Task<IEnumerable<ApplicationUser>> GetUsers()
        {
            var users = await _connection.QueryAsync<ApplicationUser>("SELECT * FROM [RadzenCRM].[dbo].[AspNetUsers]");
            return users.ToList();
        }

        public async Task<(IEnumerable<ApplicationUser> Users, int Total)> GetUsersPagedAsync(
            IEnumerable<FilterDescriptor> filters,
            IEnumerable<SortDescriptor> sorts,
            int skip, int top)
        {
            var parameters = new DynamicParameters();
            var where    = BuildWhereClause(filters, parameters);
            var orderBy  = BuildOrderByClause(sorts);

            var whereStr   = string.IsNullOrEmpty(where)    ? "" : $" WHERE {where}";
            var orderByStr = string.IsNullOrEmpty(orderBy)  ? "[UserName] ASC" : orderBy;

            var countSql = $"SELECT COUNT(*) FROM [dbo].[AspNetUsers]{whereStr}";
            var dataSql  = $@"SELECT * FROM [dbo].[AspNetUsers]{whereStr}
                              ORDER BY {orderByStr}
                              OFFSET {skip} ROWS FETCH NEXT {top} ROWS ONLY";

            var total = await _connection.ExecuteScalarAsync<int>(countSql, parameters);
            var users = await _connection.QueryAsync<ApplicationUser>(dataSql, parameters);

            return (users.ToList(), total);
        }

        private static string BuildWhereClause(IEnumerable<FilterDescriptor> filters, DynamicParameters parameters)
        {
            if (filters == null) return null;

            var parts = new List<string>();
            int i = 0;

            foreach (var f in filters)
            {
                bool needsValue = f.FilterOperator is not (
                    FilterOperator.IsNull or FilterOperator.IsNotNull or
                    FilterOperator.IsEmpty or FilterOperator.IsNotEmpty);

                if (needsValue && f.FilterValue == null) continue;

                var firstPart = FilterToSql(f.Property, f.FilterOperator, f.FilterValue, parameters, ref i);

                string part;
                if (f.SecondFilterValue != null)
                {
                    var secondPart = FilterToSql(f.Property, f.SecondFilterOperator, f.SecondFilterValue, parameters, ref i);
                    var innerOp = f.LogicalFilterOperator == LogicalFilterOperator.Or ? "OR" : "AND";
                    part = $"({firstPart} {innerOp} {secondPart})";
                }
                else
                {
                    part = firstPart;
                }

                parts.Add(part);
            }

            return parts.Count == 0 ? null : string.Join(" AND ", parts);
        }

        private static string FilterToSql(string property, FilterOperator op, object value,
                                          DynamicParameters parameters, ref int i)
        {
            var col   = $"[{property}]";
            var pName = $"@fp{i++}";

            return op switch
            {
                FilterOperator.Equals             => Param($"{col} = {pName}",            pName, value,            parameters),
                FilterOperator.NotEquals          => Param($"{col} != {pName}",           pName, value,            parameters),
                FilterOperator.LessThan           => Param($"{col} < {pName}",            pName, value,            parameters),
                FilterOperator.LessThanOrEquals   => Param($"{col} <= {pName}",           pName, value,            parameters),
                FilterOperator.GreaterThan        => Param($"{col} > {pName}",            pName, value,            parameters),
                FilterOperator.GreaterThanOrEquals=> Param($"{col} >= {pName}",           pName, value,            parameters),
                FilterOperator.Contains           => Param($"{col} LIKE {pName}",         pName, $"%{value}%",     parameters),
                FilterOperator.DoesNotContain     => Param($"{col} NOT LIKE {pName}",     pName, $"%{value}%",     parameters),
                FilterOperator.StartsWith         => Param($"{col} LIKE {pName}",         pName, $"{value}%",      parameters),
                FilterOperator.EndsWith           => Param($"{col} LIKE {pName}",         pName, $"%{value}",      parameters),
                FilterOperator.IsNull             => $"{col} IS NULL",
                FilterOperator.IsNotNull          => $"{col} IS NOT NULL",
                FilterOperator.IsEmpty            => $"{col} = ''",
                FilterOperator.IsNotEmpty         => $"({col} IS NOT NULL AND {col} != '')",
                _                                 => Param($"{col} = {pName}",            pName, value,            parameters),
            };
        }

        private static string Param(string sql, string pName, object value, DynamicParameters parameters)
        {
            parameters.Add(pName, value);
            return sql;
        }

        private static string BuildOrderByClause(IEnumerable<SortDescriptor> sorts)
        {
            if (sorts == null || !sorts.Any()) return null;
            return string.Join(", ", sorts.Select(s =>
                $"[{s.Property}] {(s.SortOrder == SortOrder.Descending ? "DESC" : "ASC")}"));
        }

        public async Task<ApplicationUser> CreateUser(ApplicationUser user)
        {
            var uri = new Uri(baseUri, $"ApplicationUsers");

            var content = new StringContent(JsonSerializer.Serialize(user), Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(uri, content);

            return await response.ReadAsync<ApplicationUser>();
        }

        public async Task<HttpResponseMessage> DeleteUser(string id)
        {

            string sql = "DELETE FROM [RadzenCRM].[dbo].[AspNetUsers] WHERE Id = @Id";

            int rowsAffected = await _connection.ExecuteAsync(sql, new { Id = id });

            return new HttpResponseMessage() { StatusCode =  System.Net.HttpStatusCode.OK };

        }

        public async Task<ApplicationUser> GetUserById(string id)
        {
            var usr = await _connection.QueryFirstOrDefaultAsync<ApplicationUser>(
                "SELECT * FROM [dbo].[AspNetUsers] WHERE Id = @Id", new { Id = id });

            if (usr == null) return null;

            usr.Roles = (await _connection.QueryAsync<ApplicationRole>(@"
                SELECT r.Id, r.Name
                FROM [dbo].[AspNetRoles] r
                INNER JOIN [dbo].[AspNetUserRoles] ur ON ur.RoleId = r.Id
                WHERE ur.UserId = @UserId", new { UserId = id })).ToList();

            return usr;
        }

        public async Task<ApplicationUser> UpdateUser(string id, ApplicationUser user)
        {
            // Clear ALL tracked entities accumulated during the Blazor Server circuit lifetime.
            // Without this, stale ApplicationRole instances from earlier reads remain in the
            // tracker and conflict with the ones UserManager loads internally.
            securitDbContext.ChangeTracker.Clear();

            var entity = await _userManager.FindByIdAsync(id);
            if (entity == null) return null;

            // Update scalar properties
            entity.Email          = user.Email;
            entity.FirstName      = user.FirstName;
            entity.LastName       = user.LastName;
            entity.Picture        = user.Picture;
            entity.IsWindowsUser  = user.IsWindowsUser;
            entity.NormalizedEmail = user.Email?.ToUpperInvariant();

            var updateResult = await _userManager.UpdateAsync(entity);
            if (!updateResult.Succeeded)
                throw new InvalidOperationException(
                    string.Join(", ", updateResult.Errors.Select(e => e.Description)));

            // Sync roles: remove all current, then add the selected ones
            if (user.Roles != null)
            {
                var currentRoles = await _userManager.GetRolesAsync(entity);
                await _userManager.RemoveFromRolesAsync(entity, currentRoles);
                var newRoleNames = user.Roles.Select(r => r.Name).Where(n => n != null);
                if (newRoleNames.Any())
                    await _userManager.AddToRolesAsync(entity, newRoleNames);
            }

            // Update password only for local users when a new password was provided
            if (!user.IsWindowsUser && !string.IsNullOrEmpty(user.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(entity);
                var pwResult = await _userManager.ResetPasswordAsync(entity, token, user.Password);
                if (!pwResult.Succeeded)
                    throw new InvalidOperationException(
                        string.Join(", ", pwResult.Errors.Select(e => e.Description)));
            }

            return entity;
        }
        public async Task ChangePassword(string oldPassword, string newPassword)
        {
            var uri =  new Uri($"{navigationManager.BaseUri}Account/ChangePassword");

            var content = new FormUrlEncodedContent(new Dictionary<string, string> {
                { "oldPassword", oldPassword },
                { "newPassword", newPassword }
            });

            var response = await httpClient.PostAsync(uri, content);

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();

                throw new ApplicationException(message);
            }
        }

        public record AdUserInfo(string SamAccountName, string DisplayName, string FirstName, string LastName, string Email, string Sid = "")
        {
            public string DisplayText => string.IsNullOrEmpty(DisplayName)
                ? SamAccountName
                : $"{SamAccountName}  —  {DisplayName}";
        }

        public async Task<IEnumerable<AdUserInfo>> SearchAdUsers(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
                return Enumerable.Empty<AdUserInfo>();

            return await Task.Run(() =>
            {
                var results = new List<AdUserInfo>();
                try
                {
                    using var entry = new DirectoryEntry("LDAP://fincaint.local");
                    using var searcher = new DirectorySearcher(entry)
                    {
                        Filter = $"(&(objectClass=user)(objectCategory=person)" +
                                 $"(!(userAccountControl:1.2.840.113556.1.4.803:=2))" +
                                 $"(|(sAMAccountName={searchTerm}*)(displayName=*{searchTerm}*)(givenName={searchTerm}*)(sn={searchTerm}*)))",
                        SizeLimit = 25
                    };
                    searcher.PropertiesToLoad.AddRange(new[] { "sAMAccountName", "displayName", "givenName", "sn", "mail", "objectSid" });

                    foreach (SearchResult r in searcher.FindAll())
                    {
                        var sam = Prop(r, "sAMAccountName");
                        if (string.IsNullOrEmpty(sam)) continue;

                        // Parse objectSid
                        string sid = "";
                        if (r.Properties["objectSid"]?.Count > 0 && r.Properties["objectSid"][0] is byte[] sidBytes)
                            sid = new SecurityIdentifier(sidBytes, 0).Value;
                        results.Add(new AdUserInfo(sam, Prop(r, "displayName"), Prop(r, "givenName"), Prop(r, "sn"), Prop(r, "mail"), sid));
                    }
                }
                catch { /* AD not available — return empty */ }
                return results.OrderBy(u => u.SamAccountName).ToList();
            });

            static string Prop(SearchResult r, string key) =>
                r.Properties[key]?.Count > 0 ? r.Properties[key][0]?.ToString() ?? "" : "";
        }

        /// <summary>Find a local DB user by their Windows SID.</summary>
        public async Task<ApplicationUser> GetUserBySidAsync(string sid)
        {
            if (string.IsNullOrEmpty(sid)) return null;
            return await _connection.QueryFirstOrDefaultAsync<ApplicationUser>(
                "SELECT * FROM [dbo].[AspNetUsers] WHERE [Sid] = @Sid", new { Sid = sid });
        }

        /// <summary>Query AD synchronously for a user by sAMAccountName and return their current info.</summary>
        public AdUserInfo GetAdInfoBySamAccountName(string samAccountName)
        {
            if (string.IsNullOrWhiteSpace(samAccountName)) return null;
            try
            {
                using var entry = new DirectoryEntry("LDAP://fincaint.local");
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectClass=user)(objectCategory=person)(sAMAccountName={samAccountName}))",
                    SizeLimit = 1
                };
                searcher.PropertiesToLoad.AddRange(new[] { "sAMAccountName", "displayName", "givenName", "sn", "mail", "objectSid" });
                var r = searcher.FindOne();
                if (r == null) return null;

                string sid = "";
                if (r.Properties["objectSid"]?.Count > 0 && r.Properties["objectSid"][0] is byte[] sidBytes)
                    sid = new SecurityIdentifier(sidBytes, 0).Value;

                return new AdUserInfo(
                    Prop2(r, "sAMAccountName"), Prop2(r, "displayName"),
                    Prop2(r, "givenName"), Prop2(r, "sn"), Prop2(r, "mail"), sid);
            }
            catch { return null; }

            static string Prop2(SearchResult r, string key) =>
                r.Properties[key]?.Count > 0 ? r.Properties[key][0]?.ToString() ?? "" : "";
        }
    }
}