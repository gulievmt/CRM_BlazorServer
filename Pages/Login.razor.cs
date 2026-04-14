using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Radzen;
using Radzen.Blazor;

namespace CRMBlazorServerRBS.Pages
{
    public partial class Login
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
        protected IConfiguration Configuration { get; set; }

        protected string redirectUrl;
        protected string error;
        protected string info;
        protected bool errorVisible;
        protected bool infoVisible;

        protected bool WindowsAuthEnabled { get; set; }

        [Inject]
        protected SecurityService Security { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var query = System.Web.HttpUtility.ParseQueryString(new Uri(NavigationManager.ToAbsoluteUri(NavigationManager.Uri).ToString()).Query);

            error = query.Get("error");

            info = query.Get("info");

            redirectUrl = query.Get("redirectUrl");

            errorVisible = !string.IsNullOrEmpty(error);

            infoVisible = !string.IsNullOrEmpty(info);

            WindowsAuthEnabled = Configuration.GetValue<bool>("Authentication:Windows:Enabled");
        }

        protected void WindowsLoginClick()
        {
            // Полная перезагрузка страницы нужна, чтобы браузер мог выполнить
            // HTTP-переговоры (Negotiate/Kerberos/NTLM) с сервером
            NavigationManager.NavigateTo(
                $"Account/WindowsLogin?redirectUrl={Uri.EscapeDataString(redirectUrl ?? "")}",
                forceLoad: true);
        }

        string theForm = "document.forms[0]";

        protected async System.Threading.Tasks.Task SplitButton0Click(Radzen.Blazor.RadzenSplitButtonItem args)
        {
            if(args?.Text == "Sales Manager")
            {
                await SetLoginCredentials("salesmanager@demo.radzen.com", "SalesManager1@");
            }
            else if(args?.Text == "Sales Representative")
            {
                await SetLoginCredentials("salesrep@demo.radzen.com", "SalesRep1@");
            }

            await JSRuntime.InvokeVoidAsync("eval", $@"{theForm}.submit()");
        }

        protected async System.Threading.Tasks.Task SetLoginCredentials(string username, string password)
        {
            await JSRuntime.InvokeVoidAsync("eval", $@"{theForm}.Username.value = '{username}'");
            await JSRuntime.InvokeVoidAsync("eval", $@"{theForm}.Password.value = '{password}'");
        }
    }
}