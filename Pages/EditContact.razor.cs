using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Radzen;
using Radzen.Blazor;

namespace CRMBlazorServerRBS.Pages
{
    public partial class EditContact
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
        [Inject]
        public RadzenCRMService RadzenCRMService { get; set; }

        [Parameter]
        public int Id { get; set; }

        protected override async Task OnInitializedAsync()
        {
            contact = await RadzenCRMService.GetContactById(Id);
        }
        protected bool errorVisible;
        protected bool concurrencyErrorVisible;
        protected CRMBlazorServerRBS.Models.RadzenCRM.Contact contact;

        [Inject]
        protected SecurityService Security { get; set; }

        protected async Task FormSubmit()
        {
            try
            {
                errorVisible = false;
                concurrencyErrorVisible = false;

                await RadzenCRMService.UpdateContact(Id, contact);
                DialogService.Close(contact);
            }
            catch (DBConcurrencyException)
            {
                // Запись изменена другим пользователем — предлагаем обновить данные
                concurrencyErrorVisible = true;
            }
            catch (Exception)
            {
                errorVisible = true;
            }
        }

        /// <summary>
        /// Перечитать актуальные данные из БД и сбросить ошибку конкурентности.
        /// </summary>
        protected async Task RefreshContact()
        {
            contact = await RadzenCRMService.GetContactById(Id);
            concurrencyErrorVisible = false;
        }

        protected async Task CancelButtonClick(MouseEventArgs args)
        {
            DialogService.Close(null);
        }
    }
}