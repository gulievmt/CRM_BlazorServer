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
using Microsoft.AspNetCore.Http.HttpResults;
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


        public SecurityService(ApplicationIdentityDbContext securitContext,   NavigationManager navigationManager,
                               IHttpClientFactory factory, IDbConnection connection)
        {
            this.securitDbContext = securitContext;
            this.baseUri = new Uri($"{navigationManager.BaseUri}odata/Identity/");
            this.httpClient = factory.CreateClient("CRMBlazorServerRBS");
            this.navigationManager = navigationManager;
            this._connection = connection;
            this._connection.Open();
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

            var content = new StringContent(ODataJsonSerializer.Serialize(role), Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(uri, content);

            return await response.ReadAsync<ApplicationRole>();
        }

        public async Task<HttpResponseMessage> DeleteRole(string id)
        {
            var uri = new Uri(baseUri, $"ApplicationRoles('{id}')");

            return await httpClient.DeleteAsync(uri);
        }

        public async Task<IEnumerable<ApplicationUser>> GetUsers()
        {
            var roles = await _connection.QueryAsync<ApplicationUser>("SELECT * FROM [RadzenCRM].[dbo].[AspNetUsers]");
            return roles.ToList();



            var uri = new Uri(baseUri, $"ApplicationUsers");

            

            uri = uri.GetODataUri();

            var response = await httpClient.GetAsync(uri);

            var result = await response.ReadAsync<ODataServiceResult<ApplicationUser>>();

            return result.Value;
            /*
SELECT TOP (1000) [Id]
      ,[AccessFailedCount]
      ,[ConcurrencyStamp]
      ,[Email]
      ,[EmailConfirmed]
      ,[LockoutEnabled]
      ,[LockoutEnd]
      ,[NormalizedEmail]
      ,[NormalizedUserName]
      ,[PasswordHash]
      ,[PhoneNumber]
      ,[PhoneNumberConfirmed]
      ,[SecurityStamp]
      ,[TwoFactorEnabled]
      ,[UserName]
      ,[FirstName]
      ,[LastName]
      ,[Picture]
  FROM [RadzenCRM].[dbo].[AspNetUsers]             
             
             
             
             */



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
            var usr = securitDbContext.ApplicationUser.Where(u => u.Id == id).FirstOrDefault();
            return usr;
        }

        public async Task<ApplicationUser> UpdateUser(string id, ApplicationUser user)
        {
            var entity = securitDbContext.ApplicationUser.Find(id);
            if (entity == null)
            {
                return null;
            }

            //entity.UserName = user.UserName;
            //entity.Email = user.Email;
            //entity.PhoneNumber = user.PhoneNumber;
            //entity.PasswordHash = user.PasswordHash;
            //entity.SecurityStamp = user.SecurityStamp;  
            //entity.AccessFailedCount = user.AccessFailedCount;
            //entity.ConcurrencyStamp = user.ConcurrencyStamp;
            //entity.EmailConfirmed = user.EmailConfirmed;
            //entity.LockoutEnabled = user.LockoutEnabled;


            securitDbContext.Entry(entity).CurrentValues.SetValues(user);
            securitDbContext.SaveChanges(); 


            return user;


            var uri = new Uri(baseUri, $"ApplicationUsers('{id}')");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Patch, uri)
            {
                Content = new StringContent(JsonSerializer.Serialize(user), Encoding.UTF8, "application/json")
            };

            var response = await httpClient.SendAsync(httpRequestMessage);

            return await response.ReadAsync<ApplicationUser>();
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
    }
}