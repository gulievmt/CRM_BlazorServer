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
    public partial class ApplicationUsers
    {
        [Inject]
        protected IJSRuntime JSRuntime { get; set; }

        [Inject]
        protected NavigationManager NavigationManager { get; set; }

        [Inject]
        protected DialogService DialogService { get; set; }

        [Inject]
        protected TooltipService TooltipService { get; set; }

        [Inject]
        protected ContextMenuService ContextMenuService { get; set; }

        [Inject]
        protected NotificationService NotificationService { get; set; }

        protected IEnumerable<CRMBlazorServerRBS.Models.ApplicationUser> users;
        protected int count;
        protected RadzenDataGrid<CRMBlazorServerRBS.Models.ApplicationUser> grid0;
        protected string error;
        protected bool errorVisible;

        // ── Custom filter for IsWindowsUser column ──────────────────────────
        protected string userTypeFilter = "all";

        protected record UserTypeOption(string Value, string Label, string Icon);
        protected static readonly IEnumerable<UserTypeOption> UserTypeOptions = new[]
        {
            new UserTypeOption("all",   "Все",              "filter_list"),
            new UserTypeOption("local", "Локальный",        "person"),
            new UserTypeOption("ad",    "Active Directory", "computer"),
        };

        [Inject]
        protected SecurityService Security { get; set; }

        protected async Task LoadData(LoadDataArgs args)
        {
            var result = await Security.GetUsersPagedAsync(
                args.Filters, args.Sorts, args.Skip ?? 0, args.Top ?? 10);

            users = result.Users;
            count = result.Total;

            await InvokeAsync(StateHasChanged);
        }

        private async Task ReloadUsers()
        {
            await grid0.Reload();
        }

        protected async Task AddClick()
        {
            await DialogService.OpenAsync<AddApplicationUser>("Add Application User");
            await ReloadUsers();
        }

        protected async Task RowSelect(CRMBlazorServerRBS.Models.ApplicationUser user)
        {
            await DialogService.OpenAsync<EditApplicationUser>("Edit Application User", new Dictionary<string, object>{ {"Id", user.Id} });
            await ReloadUsers();
        }

        protected async Task DeleteClick(CRMBlazorServerRBS.Models.ApplicationUser user)
        {
            try
            {
                if (await DialogService.Confirm("Are you sure you want to delete this user?") == true)
                {
                    await Security.DeleteUser($"{user.Id}");
                    await ReloadUsers();
                }
            }
            catch (Exception ex)
            {
                errorVisible = true;
                error = ex.Message;
            }
        }
    }
}