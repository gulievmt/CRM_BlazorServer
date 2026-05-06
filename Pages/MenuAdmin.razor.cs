using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using CRMBlazorServerRBS.Models.Menu;
using CRMBlazorServerRBS.Services;

namespace CRMBlazorServerRBS.Pages
{
    public partial class MenuAdmin
    {
        [Inject] protected IJSRuntime JSRuntime { get; set; }
        [Inject] protected NavigationManager NavigationManager { get; set; }
        [Inject] protected DialogService DialogService { get; set; }
        [Inject] protected TooltipService TooltipService { get; set; }
        [Inject] protected ContextMenuService ContextMenuService { get; set; }
        [Inject] protected NotificationService NotificationService { get; set; }
        [Inject] protected SecurityService Security { get; set; }
        [Inject] protected MenuService MenuService { get; set; }

        protected List<MenuItem> menuItems = new();
        public IList<MenuItem> selectedItems = new List<MenuItem>();
        protected RadzenDataGrid<MenuItem> grid0;
        protected string error;
        protected bool errorVisible;

        protected override async Task OnInitializedAsync()
        {
            await Reload();
            selectedItems = new List<MenuItem>() { menuItems.FirstOrDefault() };
        }

        private async Task Reload()
        {
            var flat = await MenuService.GetAllMenuItemsFlatAsync();

            // Build hierarchical order: each top-level item followed by its children
            var ordered = new List<MenuItem>();
            foreach (var parent in flat.Where(m => m.ParentId == null).OrderBy(m => m.SortOrder))
            {
                ordered.Add(parent);
                var children = flat.Where(m => m.ParentId == parent.Id).OrderBy(m => m.SortOrder);
                parent.HasChildren = children.Any();
                ordered.AddRange(children);
            }
            // Append any orphaned children that didn't match a parent
            ordered.AddRange(flat.Where(m => m.ParentId != null && !ordered.Contains(m)));

            menuItems = ordered;
        }

        protected async Task AddClick()
        {
            await DialogService.OpenAsync<MenuItemDialog>(
                "Добавить пункт меню",
                new Dictionary<string, object>
                {
                    { "MenuItemId", 0 },
                    { "AllMenuItems", menuItems }
                });
            await Reload();
            await grid0.Reload();


        }

        protected async Task EditClick(MenuItem item)
        {
            await DialogService.OpenAsync<MenuItemDialog>(
                "Редактировать пункт меню",
                new Dictionary<string, object>
                {
                    { "MenuItemId", item.Id },
                    { "AllMenuItems", menuItems }
                });
           // await Reload();
            await grid0.Reload();

            var menuItem_new = await MenuService.GetMenuItemByIdAsync(item.Id);


//            item.Text = menuItem_new.Text;
            item.CopyFrom( menuItem_new);

            selectedItems = new List<MenuItem>() { item };
            //             selectedItems = new List<MenuItem>() { menuItems.FirstOrDefault() };

        }


        protected static string ScopeLabel(string scope) => scope switch
        {
            "own"            => "Свои",
            "directreportees"=> "Подчинённые",
            "branch"         => "Филиал",
            "department"     => "Отдел",
            _                => "Все"
        };

        protected static string PermissionLabel(string permission) => permission switch
        {
            "readwrite" => "Чтение/Запись",
            "approve"   => "Утверждение",
            _           => "Чтение"
        };

        protected async Task DeleteClick(MenuItem item)
        {
            try
            {
                if (await DialogService.Confirm(
                    $"Удалить «{item.Text}»? Дочерние пункты станут верхнеуровневыми.",
                    "Подтверждение удаления") == true)
                {
                    await MenuService.DeleteMenuItemAsync(item.Id);
                    await Reload();
                    await grid0.Reload();
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
