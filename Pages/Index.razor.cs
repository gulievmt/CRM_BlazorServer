using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;
using System.Data;
using Dapper;
using Radzen;
using Radzen.Blazor;

namespace CRMBlazorServerRBS.Pages
{
    public partial class Index
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
        protected SecurityService Security { get; set; }

        [Inject]
        public IDbConnection DbConnection { get; set; }

        [Inject]
        public RadzenCRMService RadzenCRMService { get; set; }

        protected override async Task OnInitializedAsync()
        {
            monthlyStats = await MonthlyStats();
            revenueByCompany = await RevenueByCompany();
            revenueByMonth = await RevenueByMonth();
            revenueByEmployee = await RevenueByEmployee();
            getOpportunitiesResult = await RadzenCRMService.GetOpportunities(new Radzen.Query { OrderBy = "CloseDate desc", Expand = "Contact,OpportunityStatus" });
            getTasksResult = await RadzenCRMService.GetTasks(new Radzen.Query { OrderBy = "DueDate desc" });
        }

        public Stats monthlyStats { get; set; }
        IEnumerable<Pages.RevenueByCompany> revenueByCompany { get; set; }
        IEnumerable<Pages.RevenueByMonth> revenueByMonth { get; set; }
        IEnumerable<Pages.RevenueByEmployee> revenueByEmployee { get; set; }
        IQueryable<Models.RadzenCRM.Opportunity> getOpportunitiesResult { get; set; }
        IQueryable<Models.RadzenCRM.Task> getTasksResult { get; set; }

        public async Task<Stats> MonthlyStats()
        {
            var opportunities = (await DbConnection.QueryAsync<dynamic>(
                @"SELECT o.Amount, o.CloseDate, s.Name AS StatusName
                  FROM [dbo].[Opportunities] o
                  JOIN [dbo].[OpportunityStatuses] s ON o.StatusId = s.Id")).ToList();

            var totalOpportunities = opportunities.Count;
            var wonOpportunities = opportunities.Where(o => o.StatusName == "Won").ToList();
            var ratio = totalOpportunities > 0 ? (double)wonOpportunities.Count / totalOpportunities : 0;

            return wonOpportunities
                .GroupBy(o => new DateTime(((DateTime)o.CloseDate).Year, ((DateTime)o.CloseDate).Month, 1))
                .Select(group => new Stats()
                {
                    Month = group.Key,
                    Revenue = group.Sum(o => (decimal)o.Amount),
                    Opportunities = group.Count(),
                    AverageDealSize = group.Average(o => (decimal)o.Amount),
                    Ratio = ratio
                })
                .OrderBy(s => s.Month)
                .LastOrDefault();
        }

        public async Task<IEnumerable<RevenueByCompany>> RevenueByCompany()
        {
            var rows = await DbConnection.QueryAsync<dynamic>(
                @"SELECT c.Company, o.Amount
                  FROM [dbo].[Opportunities] o
                  JOIN [dbo].[Contacts] c ON o.ContactId = c.Id");

            return rows
                .GroupBy(r => (string)r.Company)
                .Select(g => new RevenueByCompany()
                {
                    Company = g.Key,
                    Revenue = g.Sum(r => (decimal)r.Amount)
                });
        }

        public async Task<IEnumerable<RevenueByEmployee>> RevenueByEmployee()
        {
            var rows = await DbConnection.QueryAsync<dynamic>(
                @"SELECT u.FirstName, u.LastName, o.Amount
                  FROM [dbo].[Opportunities] o
                  JOIN [dbo].[AspNetUsers] u ON o.UserId = u.Id");

            return rows
                .GroupBy(r => $"{r.FirstName} {r.LastName}")
                .Select(g => new RevenueByEmployee()
                {
                    Employee = g.Key,
                    Revenue = g.Sum(r => (decimal)r.Amount)
                });
        }

        public async Task<IEnumerable<RevenueByMonth>> RevenueByMonth()
        {
            var rows = await DbConnection.QueryAsync<dynamic>(
                @"SELECT o.Amount, o.CloseDate
                  FROM [dbo].[Opportunities] o
                  JOIN [dbo].[OpportunityStatuses] s ON o.StatusId = s.Id
                  WHERE s.Name = 'Won'");

            return rows
                .GroupBy(r => new DateTime(((DateTime)r.CloseDate).Year, ((DateTime)r.CloseDate).Month, 1))
                .Select(g => new RevenueByMonth()
                {
                    Revenue = g.Sum(r => (decimal)r.Amount),
                    Month = g.Key
                })
                .OrderBy(r => r.Month);
        }
    }

    public class Stats
    {
       public DateTime Month { get; set; }
       public decimal Revenue { get; set; }

       public int Opportunities { get; set; }
       public decimal AverageDealSize { get; set; }
       public double Ratio { get; set; }
    }

    public class RevenueByCompany
    {
            public string Company { get; set; }
            public decimal Revenue { get; set; }
    }

    public class RevenueByEmployee
    {
            public string Employee { get; set; }
            public decimal Revenue { get; set; }
    }

    public class RevenueByMonth
    {
            public DateTime Month { get; set; }
            public decimal Revenue { get; set; }
    }
}
