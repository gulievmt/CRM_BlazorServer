using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Radzen;
using Radzen.Blazor;

namespace CRMBlazorServerRBS.Pages
{
    public partial class AddApplicationUser
    {
        [Inject] protected IJSRuntime JSRuntime { get; set; }
        [Inject] protected NavigationManager NavigationManager { get; set; }
        [Inject] protected DialogService DialogService { get; set; }
        [Inject] protected TooltipService TooltipService { get; set; }
        [Inject] protected ContextMenuService ContextMenuService { get; set; }
        [Inject] protected NotificationService NotificationService { get; set; }
        [Inject] protected SecurityService Security { get; set; }

        protected IEnumerable<CRMBlazorServerRBS.Models.ApplicationRole> roles;
        protected CRMBlazorServerRBS.Models.ApplicationUser user;
        protected IEnumerable<string> userRoles = Enumerable.Empty<string>();
        protected string error;
        protected bool errorVisible;

        // Computed string value for RadzenSelectBar ("local" / "ad")
        private string UserTypeValue
        {
            get => user?.IsWindowsUser == true ? "ad" : "local";
            set
            {
                var isAd = value == "ad";
                if (user != null) user.IsWindowsUser = isAd;
                OnIsWindowsUserChanged(isAd);
            }
        }

        // AD autocomplete state
        private string adSearchText;
        private IEnumerable<SecurityService.AdUserInfo> adSuggestions = Enumerable.Empty<SecurityService.AdUserInfo>();
        private SecurityService.AdUserInfo selectedAdUser;

        protected override async Task OnInitializedAsync()
        {
            user = new CRMBlazorServerRBS.Models.ApplicationUser();
            roles = await Security.GetRoles();
        }

        // Called when IsWindowsUser toggle changes
        private void OnIsWindowsUserChanged(bool value)
        {
            adSearchText = null;
            selectedAdUser = null;
            adSuggestions = Enumerable.Empty<SecurityService.AdUserInfo>();
            if (value)
            {
                user.UserName = null;
                user.Email = null;
                user.FirstName = null;
                user.LastName = null;
            }
        }

        // Called by RadzenAutoComplete LoadData event
        private async Task OnAdSearchLoad(LoadDataArgs args)
        {
            adSuggestions = await Security.SearchAdUsers(args.Filter ?? "");
            await InvokeAsync(StateHasChanged);
        }

        // Called when user picks a suggestion from the AD autocomplete
        private void OnAdUserSelected(object value)
        {
            // Find the AdUserInfo that matches the displayed text
            var displayText = value?.ToString();
            selectedAdUser = adSuggestions.FirstOrDefault(u => u.DisplayText == displayText);
            if (selectedAdUser != null)
            {
                user.UserName  = selectedAdUser.SamAccountName;
                user.Email     = selectedAdUser.Email;
                user.FirstName = selectedAdUser.FirstName;
                user.LastName  = selectedAdUser.LastName;
            }
        }

        protected async Task FormSubmit(CRMBlazorServerRBS.Models.ApplicationUser user)
        {
            try
            {
                // Validate AD user was selected when IsWindowsUser = true
                if (user.IsWindowsUser && string.IsNullOrEmpty(user.UserName))
                {
                    errorVisible = true;
                    error = "Выберите пользователя из Active Directory.";
                    return;
                }

                user.Roles = roles.Where(role => userRoles.Contains(role.Id)).ToList();
                await Security.CreateUser(user);
                DialogService.Close(null);
            }
            catch (Exception ex)
            {
                errorVisible = true;
                error = ex.Message;
            }
        }

        protected async Task CancelClick()
        {
            DialogService.Close(null);
        }
    }
}
