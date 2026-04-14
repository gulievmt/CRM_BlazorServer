using CRMBlazorServerRBS.CustomCodes;
using DataHelper.Extensions;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace CRMBlazorServerRBS.Pages
{
    public partial class Contacts
    {
        [Inject] AuditService Audit { get; set; }


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

        protected IEnumerable<CRMBlazorServerRBS.Models.RadzenCRM.Contact> contacts;

        protected RadzenDataGrid<CRMBlazorServerRBS.Models.RadzenCRM.Contact> grid0;

        protected string search = "";

        public int count;

        [Inject]
        protected SecurityService Security { get; set; }

        protected async Task Search(ChangeEventArgs args)
        {
            search = $"{args.Value}";

            if(grid0.CurrentPage != 0)
            await grid0.GoToPage(0);
            else
           await grid0.Reload();

            //contacts = await RadzenCRMService.GetContactsPaged(new Query { Filter = $@"i => i.Email.Contains(@0) || i.Company.Contains(@0) || i.LastName.Contains(@0) || i.FirstName.Contains(@0) || i.Phone.Contains(@0)", FilterParameters = new object[] { search } });
        }
        protected override async Task OnInitializedAsync()
        {/*
            var query = new Query { Filter = $@"i => i.Email.Contains(@0) || i.Company.Contains(@0) || i.LastName.Contains(@0) || i.FirstName.Contains(@0) || i.Phone.Contains(@0)", FilterParameters = new object[] { search } };
            contacts = (await RadzenCRMService.GetContacts( query)).Take(5);
            contacts = contacts.Take(5);
            */
        }


        protected async Task AddButtonClick(MouseEventArgs args)
        {
           bool ok = await DialogService.OpenAsync<AddContact>("Add Contact", null)??false;
           if(ok) await grid0.Reload();
            Audit.Log("AddButtonClick", "");
        }

        private bool isInEditRow = false;
        protected async Task EditRow(CRMBlazorServerRBS.Models.RadzenCRM.Contact args)
        {
            if(isInEditRow) return; 
            
            try
            {
                isInEditRow = true;
                var result = await DialogService.OpenAsync<EditContact>("Edit Contact", new Dictionary<string, object> { { "Id", args.Id } });
                if (result != null)
                {
                    await grid0.Reload();

                    // Восстанавливаем выборку строки после перезагрузки
                    var updatedItem = contacts?.FirstOrDefault(c => c.Id == args.Id);
                    if (updatedItem != null)
                        await grid0.SelectRow(updatedItem);
                }
            }
            finally
            {
                isInEditRow = false;
            }
        }

        protected async Task GridDeleteButtonClick(MouseEventArgs args, CRMBlazorServerRBS.Models.RadzenCRM.Contact contact)
        {
            Audit.Log("GridDeleteButtonClick", contact.Id.ToString());

            try
            {
                if (await DialogService.Confirm("Are you sure you want to delete this record?") == true)
                {
                    var deleteResult = await RadzenCRMService.DeleteContact(contact.Id);

                    if (deleteResult != null)
                    {
                        await grid0.Reload();
                    }
                }
            }
            catch (Exception ex)
            {
                NotificationService.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = $"Error",
                    Detail = $"Unable to delete Contact"
                });
            }
        }

        protected async Task ExportClick(RadzenSplitButtonItem args)
        {
            if (args?.Value == "csv")
            {
                await RadzenCRMService.ExportContactsToCSV(new Query
                {
                    Filter = $@"{(string.IsNullOrEmpty(grid0.Query.Filter) ? "true" : grid0.Query.Filter)}",
                    OrderBy = $"{grid0.Query.OrderBy}",
                    Expand = "",
                    Select = string.Join(",", grid0.ColumnsCollection.Where(c => c.GetVisible() && !string.IsNullOrEmpty(c.Property)).Select(c => c.Property.Contains(".") ? c.Property + " as " + c.Property.Replace(".", "") : c.Property))
                }, "Contacts");
            }

            if (args == null || args.Value == "xlsx")
            {
                await RadzenCRMService.ExportContactsToExcel(new Query
                {
                    Filter = $@"{(string.IsNullOrEmpty(grid0.Query.Filter) ? "true" : grid0.Query.Filter)}",
                    OrderBy = $"{grid0.Query.OrderBy}",
                    Expand = "",
                    Select = string.Join(",", grid0.ColumnsCollection.Where(c => c.GetVisible() && !string.IsNullOrEmpty(c.Property)).Select(c => c.Property.Contains(".") ? c.Property + " as " + c.Property.Replace(".", "") : c.Property))
                }, "Contacts");
            }
        }

        async Task LoadData(LoadDataArgs args)
        {
            var query = new Query
            {
                Skip = args.Skip,
                Top = args.Top,
                Filter = args.Filter,
                OrderBy = args.OrderBy
            };

        
            var filter_params = args.Filters.ToSqlWhereClause();

            string whereClause = filter_params.whereClause;
            var parameters = filter_params.parameters;

            if( !string.IsNullOrEmpty(search))
            {
                if (!string.IsNullOrEmpty(whereClause))
                {
                    whereClause += " AND ";
                }
                whereClause += "( Email like '%' +@search + '%' or  Company like '%' +@search + '%' or LastName like '%' +@search + '%' or FirstName like '%' +@search + '%' or Phone like '%' +@search + '%')";
                parameters.Add("@search", search);
            }

            var result = await RadzenCRMService.GetContactsPaged(query, whereClause, parameters);
            contacts = result.Items;
            count = result.Count;
        }
    }
}