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
            model.SelectedRoles.Add(new MenuItemRoleAssignment
            {
                RoleName   = availableRoles.FirstOrDefault(),
                Scope      = "all",
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
    }
}
