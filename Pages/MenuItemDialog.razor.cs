using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using CRMBlazorServerRBS.Models.Menu;
using CRMBlazorServerRBS.Services;

namespace CRMBlazorServerRBS.Pages
{
    public partial class MenuItemDialog
    {
        protected bool IsSysAdmin;
        [Inject] protected IJSRuntime JSRuntime { get; set; }
        [Inject] protected NavigationManager NavigationManager { get; set; }
        [Inject] protected DialogService DialogService { get; set; }
        [Inject] protected NotificationService NotificationService { get; set; }
        [Inject] protected SecurityService Security { get; set; }
        [Inject] protected MenuService MenuService { get; set; }

        // Parameters passed from MenuAdmin via DialogService.OpenAsync
        [Parameter] public int MenuItemId { get; set; }        // 0 = new item
        [Parameter] public List<MenuItem> AllMenuItems { get; set; }

        protected MenuItemEditModel model;
        protected IEnumerable<string> availableRoles;
        // Only top-level items (ParentId == null) can be parents — prevents >1 nesting level
        protected IEnumerable<MenuItem> eligibleParents;
        protected string error;
        protected bool errorVisible;

        protected bool HasChildren = false;


        protected static readonly List<LabelValue> ScopeOptions = new()
        {
            new("Все",                    "all"),
            new("Свои",                   "own"),
            new("Прямые подчинённые",     "directreportees"),
            new("Филиал",                 "branch"),
            new("Отдел",                  "department"),
        };

        protected static readonly List<LabelValue> PermissionOptions = new()
        {
            new("Чтение",           "read"),
            new("Чтение / Запись",  "readwrite"),
            new("Утверждение",      "approve"),
        };

        protected record LabelValue(string Label, string Value);

        protected override async Task OnInitializedAsync()
        {

            IsSysAdmin = Security.IsInRole( "sysadmin" );

            availableRoles = (await Security.GetRoles()).Select(r => r.Name);

            eligibleParents = AllMenuItems
                .Where(m => m.ParentId == null && m.Id != MenuItemId)
                .OrderBy(m => m.SortOrder)
                .ToList();

            if (MenuItemId == 0)
            {
                model = new MenuItemEditModel
                {
                    IsActive = true,
                    SortOrder = AllMenuItems.Count > 0
                        ? AllMenuItems.Max(m => m.SortOrder) + 10
                        : 10
                };
            }
            else
            {
                HasChildren = AllMenuItems.Any(m => m.ParentId == MenuItemId);
                var existing = await MenuService.GetMenuItemByIdAsync(MenuItemId);
                model = new MenuItemEditModel
                {
                    Id            = existing.Id,
                    Text          = existing.Text,
                    Path          = existing.Path,
                    Icon          = existing.Icon,
                    ParentId      = existing.ParentId,
                    SortOrder     = existing.SortOrder,
                    IsActive      = existing.IsActive,
                    SelectedRoles = existing.AllowedRoles
                        .Select(r => new MenuItemRoleAssignment
                        {
                            RoleName   = r.RoleName,
                            Scope      = r.Scope,
                            Permission = r.Permission
                        }).ToList()
                };
            }
        }

        protected void AddRoleAssignment()
        {
            var usedRoles = model.SelectedRoles.Select(r => r.RoleName).ToHashSet();
            var nextRole = availableRoles.FirstOrDefault(r => !usedRoles.Contains(r));

            model.SelectedRoles.Add(new MenuItemRoleAssignment
            {
                RoleName = nextRole,
                Scope = "all",
                Permission = "read"
            });
        }

        protected void RemoveRoleAssignment(MenuItemRoleAssignment assignment)
        {
            model.SelectedRoles.Remove(assignment);
        }

        protected async Task FormSubmit(MenuItemEditModel submittedModel)
        {
            try
            {

                if (submittedModel.SelectedRoles.Select(r => r.RoleName).Distinct().Count()  != submittedModel.SelectedRoles.Count)
                {
                    errorVisible = true;
                    error = "Роль нельзя назначать более одного раза.";
                    return;
                }

                if (!IsSysAdmin)
                {
                    if (MenuItemId == 0)
                    {
                        errorVisible = true;
                        error = "Недостаточно прав для создания пункта меню.";
                        return;
                    }

                    // не-sysadmin может менять только SelectedRoles —
                    // остальные поля принудительно возвращаем к исходным значениям
                    var existing = await MenuService.GetMenuItemByIdAsync(MenuItemId);
                    submittedModel.Text = existing.Text;
                    submittedModel.Path = existing.Path;
                    submittedModel.Icon = existing.Icon;
                    submittedModel.ParentId = existing.ParentId;
                    submittedModel.SortOrder = existing.SortOrder;
                    submittedModel.IsActive = existing.IsActive;
                }

                if (MenuItemId == 0)
                    await MenuService.CreateMenuItemAsync(submittedModel);
                else
                    await MenuService.UpdateMenuItemAsync(submittedModel);

                DialogService.Close(true);
            }
            catch (Exception ex)
            {
                errorVisible = true;
                error = ex.Message;
            }
        }

        protected void CancelClick() => DialogService.Close(null);

        protected static readonly List<string> AvailableIcons = new()
{
    "home", "menu", "settings", "dashboard", "person", "people",
    "group", "lock", "lock_open", "visibility", "visibility_off",
    "edit", "delete", "add", "remove", "save", "search", "filter_list",
    "list", "grid_view", "apps", "folder", "folder_open", "description",
    "insert_drive_file", "attach_file", "cloud", "cloud_upload", "cloud_download",
    "notifications", "notifications_active", "mail", "chat", "phone",
    "calendar_today", "event", "schedule", "assignment", "task",
    "check_circle", "cancel", "warning", "info", "error",
    "arrow_back", "arrow_forward", "expand_more", "expand_less",
    "star", "star_border", "favorite", "favorite_border",
    "shopping_cart", "attach_money", "receipt", "account_balance",
    "business", "work", "store", "inventory", "local_shipping",
    "bar_chart", "pie_chart", "trending_up", "trending_down",
    "print", "download", "upload", "share", "link", "refresh",
    "logout", "login", "exit_to_app", "vpn_key", "admin_panel_settings"
};
    }
}
