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
        protected RadzenDataGrid<MenuItem> grid0;
        protected string error;
        protected bool errorVisible;

        protected override async Task OnInitializedAsync()
        {
            await Reload();
        }

        private async Task Reload()
        {
            menuItems = await MenuService.GetAllMenuItemsFlatAsync();
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
            await Reload();
            await grid0.Reload();
        }

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
