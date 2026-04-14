using System;
using System.Data;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Dapper;
using Radzen;

using CRMBlazorServerRBS.Models.RadzenCRM;

namespace CRMBlazorServerRBS
{
    public partial class RadzenCRMService
    {
        private readonly IDbConnection db;
        private readonly NavigationManager navigationManager;

        public RadzenCRMService(IDbConnection db, NavigationManager navigationManager)
        {
            this.db = db;
            this.navigationManager = navigationManager;
        }

        public void Reset() { }


        public async Task ExportContactsToExcel(Query query = null, string fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/radzencrm/contacts/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/radzencrm/contacts/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        public async Task ExportContactsToCSV(Query query = null, string fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/radzencrm/contacts/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/radzencrm/contacts/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        partial void OnContactsRead(ref IQueryable<CRMBlazorServerRBS.Models.RadzenCRM.Contact> items);

        public async Task<IQueryable<CRMBlazorServerRBS.Models.RadzenCRM.Contact>> GetContacts(Query query = null)
        {
            var contacts = await db.QueryAsync<CRMBlazorServerRBS.Models.RadzenCRM.Contact>(
                "SELECT * FROM [dbo].[Contacts]");
            var items = contacts.AsQueryable();

            if (query != null)
            {
                if (!string.IsNullOrEmpty(query.Filter))
                {
                    items = query.FilterParameters != null
                        ? items.Where(query.Filter, query.FilterParameters)
                        : items.Where(query.Filter);
                }

                if (!string.IsNullOrEmpty(query.OrderBy))
                    items = items.OrderBy(query.OrderBy);

                if (query.Skip.HasValue)
                    items = items.Skip(query.Skip.Value);

                if (query.Top.HasValue)
                    items = items.Take(query.Top.Value);
            }

            OnContactsRead(ref items);

            return await Task.FromResult(items);
        }

        partial void OnContactGet(CRMBlazorServerRBS.Models.RadzenCRM.Contact item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Contact> GetContactById(int id)
        {
            var item = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.Contact>(
                "SELECT * FROM [dbo].[Contacts] WHERE [Id] = @Id", new { Id = id });

            OnContactGet(item);

            return item;
        }

        partial void OnContactCreated(CRMBlazorServerRBS.Models.RadzenCRM.Contact item);
        partial void OnAfterContactCreated(CRMBlazorServerRBS.Models.RadzenCRM.Contact item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Contact> CreateContact(CRMBlazorServerRBS.Models.RadzenCRM.Contact contact)
        {
            OnContactCreated(contact);

            var existing = await db.QueryFirstOrDefaultAsync<int?>(
                "SELECT [Id] FROM [dbo].[Contacts] WHERE [Id] = @Id", new { contact.Id });

            if (existing != null)
                throw new Exception("Item already available");

            contact.Id = await db.QuerySingleAsync<int>(
                @"INSERT INTO [dbo].[Contacts] ([Email],[Company],[LastName],[FirstName],[Phone])
                  VALUES (@Email,@Company,@LastName,@FirstName,@Phone);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);", contact);

            OnAfterContactCreated(contact);

            return contact;
        }

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Contact> CancelContactChanges(CRMBlazorServerRBS.Models.RadzenCRM.Contact item)
        {
            return await GetContactById(item.Id);
        }

        partial void OnContactUpdated(CRMBlazorServerRBS.Models.RadzenCRM.Contact item);
        partial void OnAfterContactUpdated(CRMBlazorServerRBS.Models.RadzenCRM.Contact item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Contact> UpdateContact(int id, CRMBlazorServerRBS.Models.RadzenCRM.Contact contact)
        {
            OnContactUpdated(contact);

            var rows = await db.ExecuteAsync(
                @"UPDATE [dbo].[Contacts] SET
                    [Email]=@Email,[Company]=@Company,[LastName]=@LastName,
                    [FirstName]=@FirstName,[Phone]=@Phone
                  WHERE [Id]=@Id", contact);

            if (rows == 0)
                throw new Exception("Item no longer available");

            OnAfterContactUpdated(contact);

            return contact;
        }

        partial void OnContactDeleted(CRMBlazorServerRBS.Models.RadzenCRM.Contact item);
        partial void OnAfterContactDeleted(CRMBlazorServerRBS.Models.RadzenCRM.Contact item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Contact> DeleteContact(int id)
        {
            var item = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.Contact>(
                "SELECT * FROM [dbo].[Contacts] WHERE [Id] = @Id", new { Id = id });

            if (item == null)
                throw new Exception("Item no longer available");

            OnContactDeleted(item);

            await db.ExecuteAsync("DELETE FROM [dbo].[Contacts] WHERE [Id] = @Id", new { Id = id });

            OnAfterContactDeleted(item);

            return item;
        }

        public async Task ExportOpportunitiesToExcel(Query query = null, string fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/radzencrm/opportunities/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/radzencrm/opportunities/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        public async Task ExportOpportunitiesToCSV(Query query = null, string fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/radzencrm/opportunities/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/radzencrm/opportunities/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        partial void OnOpportunitiesRead(ref IQueryable<CRMBlazorServerRBS.Models.RadzenCRM.Opportunity> items);

        public async Task<IQueryable<CRMBlazorServerRBS.Models.RadzenCRM.Opportunity>> GetOpportunities(Query query = null)
        {
            var opportunities = (await db.QueryAsync<CRMBlazorServerRBS.Models.RadzenCRM.Opportunity>(
                "SELECT * FROM [dbo].[Opportunities]")).ToList();

            var contacts = (await db.QueryAsync<CRMBlazorServerRBS.Models.RadzenCRM.Contact>(
                "SELECT * FROM [dbo].[Contacts]")).ToDictionary(c => c.Id);

            var statuses = (await db.QueryAsync<CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus>(
                "SELECT * FROM [dbo].[OpportunityStatuses]")).ToDictionary(s => s.Id);

            var users = (await db.QueryAsync<CRMBlazorServerRBS.Models.ApplicationUser>(
                "SELECT [Id],[UserName],[Email],[FirstName],[LastName],[Picture] FROM [dbo].[AspNetUsers]"))
                .ToDictionary(u => u.Id);

            foreach (var opp in opportunities)
            {
                opp.Contact = contacts.GetValueOrDefault(opp.ContactId);
                opp.OpportunityStatus = statuses.GetValueOrDefault(opp.StatusId);
                if (!string.IsNullOrEmpty(opp.UserId))
                    opp.User = users.GetValueOrDefault(opp.UserId);
            }

            var items = opportunities.AsQueryable();

            if (query != null)
            {
                if (!string.IsNullOrEmpty(query.Filter))
                {
                    items = query.FilterParameters != null
                        ? items.Where(query.Filter, query.FilterParameters)
                        : items.Where(query.Filter);
                }

                if (!string.IsNullOrEmpty(query.OrderBy))
                    items = items.OrderBy(query.OrderBy);

                if (query.Skip.HasValue)
                    items = items.Skip(query.Skip.Value);

                if (query.Top.HasValue)
                    items = items.Take(query.Top.Value);
            }

            OnOpportunitiesRead(ref items);

            return await Task.FromResult(items);
        }

        partial void OnOpportunityGet(CRMBlazorServerRBS.Models.RadzenCRM.Opportunity item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Opportunity> GetOpportunityById(int id)
        {
            var item = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.Opportunity>(
                "SELECT * FROM [dbo].[Opportunities] WHERE [Id] = @Id", new { Id = id });

            if (item != null)
            {
                item.Contact = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.Contact>(
                    "SELECT * FROM [dbo].[Contacts] WHERE [Id] = @Id", new { Id = item.ContactId });
                item.OpportunityStatus = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus>(
                    "SELECT * FROM [dbo].[OpportunityStatuses] WHERE [Id] = @Id", new { Id = item.StatusId });
            }

            OnOpportunityGet(item);

            return item;
        }

        partial void OnOpportunityCreated(CRMBlazorServerRBS.Models.RadzenCRM.Opportunity item);
        partial void OnAfterOpportunityCreated(CRMBlazorServerRBS.Models.RadzenCRM.Opportunity item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Opportunity> CreateOpportunity(CRMBlazorServerRBS.Models.RadzenCRM.Opportunity opportunity)
        {
            OnOpportunityCreated(opportunity);

            var existing = await db.QueryFirstOrDefaultAsync<int?>(
                "SELECT [Id] FROM [dbo].[Opportunities] WHERE [Id] = @Id", new { opportunity.Id });

            if (existing != null)
                throw new Exception("Item already available");

            opportunity.Id = await db.QuerySingleAsync<int>(
                @"INSERT INTO [dbo].[Opportunities] ([Amount],[Name],[UserId],[ContactId],[StatusId],[CloseDate])
                  VALUES (@Amount,@Name,@UserId,@ContactId,@StatusId,@CloseDate);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);", opportunity);

            OnAfterOpportunityCreated(opportunity);

            return opportunity;
        }

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Opportunity> CancelOpportunityChanges(CRMBlazorServerRBS.Models.RadzenCRM.Opportunity item)
        {
            return await GetOpportunityById(item.Id);
        }

        partial void OnOpportunityUpdated(CRMBlazorServerRBS.Models.RadzenCRM.Opportunity item);
        partial void OnAfterOpportunityUpdated(CRMBlazorServerRBS.Models.RadzenCRM.Opportunity item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Opportunity> UpdateOpportunity(int id, CRMBlazorServerRBS.Models.RadzenCRM.Opportunity opportunity)
        {
            OnOpportunityUpdated(opportunity);

            var rows = await db.ExecuteAsync(
                @"UPDATE [dbo].[Opportunities] SET
                    [Amount]=@Amount,[Name]=@Name,[UserId]=@UserId,
                    [ContactId]=@ContactId,[StatusId]=@StatusId,[CloseDate]=@CloseDate
                  WHERE [Id]=@Id", opportunity);

            if (rows == 0)
                throw new Exception("Item no longer available");

            OnAfterOpportunityUpdated(opportunity);

            return opportunity;
        }

        partial void OnOpportunityDeleted(CRMBlazorServerRBS.Models.RadzenCRM.Opportunity item);
        partial void OnAfterOpportunityDeleted(CRMBlazorServerRBS.Models.RadzenCRM.Opportunity item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Opportunity> DeleteOpportunity(int id)
        {
            var item = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.Opportunity>(
                "SELECT * FROM [dbo].[Opportunities] WHERE [Id] = @Id", new { Id = id });

            if (item == null)
                throw new Exception("Item no longer available");

            OnOpportunityDeleted(item);

            await db.ExecuteAsync("DELETE FROM [dbo].[Opportunities] WHERE [Id] = @Id", new { Id = id });

            OnAfterOpportunityDeleted(item);

            return item;
        }

        public async Task ExportOpportunityStatusesToExcel(Query query = null, string fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/radzencrm/opportunitystatuses/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/radzencrm/opportunitystatuses/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        public async Task ExportOpportunityStatusesToCSV(Query query = null, string fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/radzencrm/opportunitystatuses/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/radzencrm/opportunitystatuses/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        partial void OnOpportunityStatusesRead(ref IQueryable<CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus> items);

        public async Task<IQueryable<CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus>> GetOpportunityStatuses(Query query = null)
        {
            var statuses = await db.QueryAsync<CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus>(
                "SELECT * FROM [dbo].[OpportunityStatuses]");
            var items = statuses.AsQueryable();

            if (query != null)
            {
                if (!string.IsNullOrEmpty(query.Filter))
                {
                    items = query.FilterParameters != null
                        ? items.Where(query.Filter, query.FilterParameters)
                        : items.Where(query.Filter);
                }

                if (!string.IsNullOrEmpty(query.OrderBy))
                    items = items.OrderBy(query.OrderBy);

                if (query.Skip.HasValue)
                    items = items.Skip(query.Skip.Value);

                if (query.Top.HasValue)
                    items = items.Take(query.Top.Value);
            }

            OnOpportunityStatusesRead(ref items);

            return await Task.FromResult(items);
        }

        partial void OnOpportunityStatusGet(CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus> GetOpportunityStatusById(int id)
        {
            var item = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus>(
                "SELECT * FROM [dbo].[OpportunityStatuses] WHERE [Id] = @Id", new { Id = id });

            OnOpportunityStatusGet(item);

            return item;
        }

        partial void OnOpportunityStatusCreated(CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus item);
        partial void OnAfterOpportunityStatusCreated(CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus> CreateOpportunityStatus(CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus opportunitystatus)
        {
            OnOpportunityStatusCreated(opportunitystatus);

            var existing = await db.QueryFirstOrDefaultAsync<int?>(
                "SELECT [Id] FROM [dbo].[OpportunityStatuses] WHERE [Id] = @Id", new { opportunitystatus.Id });

            if (existing != null)
                throw new Exception("Item already available");

            opportunitystatus.Id = await db.QuerySingleAsync<int>(
                @"INSERT INTO [dbo].[OpportunityStatuses] ([Name]) VALUES (@Name);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);", opportunitystatus);

            OnAfterOpportunityStatusCreated(opportunitystatus);

            return opportunitystatus;
        }

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus> CancelOpportunityStatusChanges(CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus item)
        {
            return await GetOpportunityStatusById(item.Id);
        }

        partial void OnOpportunityStatusUpdated(CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus item);
        partial void OnAfterOpportunityStatusUpdated(CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus> UpdateOpportunityStatus(int id, CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus opportunitystatus)
        {
            OnOpportunityStatusUpdated(opportunitystatus);

            var rows = await db.ExecuteAsync(
                "UPDATE [dbo].[OpportunityStatuses] SET [Name]=@Name WHERE [Id]=@Id", opportunitystatus);

            if (rows == 0)
                throw new Exception("Item no longer available");

            OnAfterOpportunityStatusUpdated(opportunitystatus);

            return opportunitystatus;
        }

        partial void OnOpportunityStatusDeleted(CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus item);
        partial void OnAfterOpportunityStatusDeleted(CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus> DeleteOpportunityStatus(int id)
        {
            var item = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.OpportunityStatus>(
                "SELECT * FROM [dbo].[OpportunityStatuses] WHERE [Id] = @Id", new { Id = id });

            if (item == null)
                throw new Exception("Item no longer available");

            OnOpportunityStatusDeleted(item);

            await db.ExecuteAsync("DELETE FROM [dbo].[OpportunityStatuses] WHERE [Id] = @Id", new { Id = id });

            OnAfterOpportunityStatusDeleted(item);

            return item;
        }

        public async Task ExportTasksToExcel(Query query = null, string fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/radzencrm/tasks/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/radzencrm/tasks/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        public async Task ExportTasksToCSV(Query query = null, string fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/radzencrm/tasks/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/radzencrm/tasks/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        partial void OnTasksRead(ref IQueryable<CRMBlazorServerRBS.Models.RadzenCRM.Task> items);

        public async Task<IQueryable<CRMBlazorServerRBS.Models.RadzenCRM.Task>> GetTasks(Query query = null)
        {
            var tasks = (await db.QueryAsync<CRMBlazorServerRBS.Models.RadzenCRM.Task>(
                "SELECT * FROM [dbo].[Tasks]")).ToList();

            var opportunities = (await db.QueryAsync<CRMBlazorServerRBS.Models.RadzenCRM.Opportunity>(
                "SELECT * FROM [dbo].[Opportunities]")).ToList();

            var contacts = (await db.QueryAsync<CRMBlazorServerRBS.Models.RadzenCRM.Contact>(
                "SELECT * FROM [dbo].[Contacts]")).ToDictionary(c => c.Id);

            var users = (await db.QueryAsync<CRMBlazorServerRBS.Models.ApplicationUser>(
                "SELECT [Id],[UserName],[Email],[FirstName],[LastName],[Picture] FROM [dbo].[AspNetUsers]"))
                .ToDictionary(u => u.Id);

            var taskStatuses = (await db.QueryAsync<CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus>(
                "SELECT * FROM [dbo].[TaskStatuses]")).ToDictionary(s => s.Id);

            var taskTypes = (await db.QueryAsync<CRMBlazorServerRBS.Models.RadzenCRM.TaskType>(
                "SELECT * FROM [dbo].[TaskTypes]")).ToDictionary(t => t.Id);

            foreach (var opp in opportunities)
            {
                opp.Contact = contacts.GetValueOrDefault(opp.ContactId);
                if (!string.IsNullOrEmpty(opp.UserId))
                    opp.User = users.GetValueOrDefault(opp.UserId);
            }

            var opportunitiesById = opportunities.ToDictionary(o => o.Id);

            foreach (var task in tasks)
            {
                task.Opportunity = opportunitiesById.GetValueOrDefault(task.OpportunityId);
                if (task.StatusId.HasValue)
                    task.TaskStatus = taskStatuses.GetValueOrDefault(task.StatusId.Value);
                task.TaskType = taskTypes.GetValueOrDefault(task.TypeId);
            }

            var items = tasks.AsQueryable();

            if (query != null)
            {
                if (!string.IsNullOrEmpty(query.Filter))
                {
                    items = query.FilterParameters != null
                        ? items.Where(query.Filter, query.FilterParameters)
                        : items.Where(query.Filter);
                }

                if (!string.IsNullOrEmpty(query.OrderBy))
                    items = items.OrderBy(query.OrderBy);

                if (query.Skip.HasValue)
                    items = items.Skip(query.Skip.Value);

                if (query.Top.HasValue)
                    items = items.Take(query.Top.Value);
            }

            OnTasksRead(ref items);

            return await Task.FromResult(items);
        }

        partial void OnTaskGet(CRMBlazorServerRBS.Models.RadzenCRM.Task item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Task> GetTaskById(int id)
        {
            var item = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.Task>(
                "SELECT * FROM [dbo].[Tasks] WHERE [Id] = @Id", new { Id = id });

            if (item != null)
            {
                item.Opportunity = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.Opportunity>(
                    "SELECT * FROM [dbo].[Opportunities] WHERE [Id] = @Id", new { Id = item.OpportunityId });
                if (item.StatusId.HasValue)
                    item.TaskStatus = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus>(
                        "SELECT * FROM [dbo].[TaskStatuses] WHERE [Id] = @Id", new { Id = item.StatusId.Value });
                item.TaskType = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.TaskType>(
                    "SELECT * FROM [dbo].[TaskTypes] WHERE [Id] = @Id", new { Id = item.TypeId });
            }

            OnTaskGet(item);

            return item;
        }

        partial void OnTaskCreated(CRMBlazorServerRBS.Models.RadzenCRM.Task item);
        partial void OnAfterTaskCreated(CRMBlazorServerRBS.Models.RadzenCRM.Task item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Task> CreateTask(CRMBlazorServerRBS.Models.RadzenCRM.Task task)
        {
            OnTaskCreated(task);

            var existing = await db.QueryFirstOrDefaultAsync<int?>(
                "SELECT [Id] FROM [dbo].[Tasks] WHERE [Id] = @Id", new { task.Id });

            if (existing != null)
                throw new Exception("Item already available");

            task.Id = await db.QuerySingleAsync<int>(
                @"INSERT INTO [dbo].[Tasks] ([Title],[OpportunityId],[DueDate],[TypeId],[StatusId])
                  VALUES (@Title,@OpportunityId,@DueDate,@TypeId,@StatusId);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);", task);

            OnAfterTaskCreated(task);

            return task;
        }

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Task> CancelTaskChanges(CRMBlazorServerRBS.Models.RadzenCRM.Task item)
        {
            return await GetTaskById(item.Id);
        }

        partial void OnTaskUpdated(CRMBlazorServerRBS.Models.RadzenCRM.Task item);
        partial void OnAfterTaskUpdated(CRMBlazorServerRBS.Models.RadzenCRM.Task item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Task> UpdateTask(int id, CRMBlazorServerRBS.Models.RadzenCRM.Task task)
        {
            OnTaskUpdated(task);

            var rows = await db.ExecuteAsync(
                @"UPDATE [dbo].[Tasks] SET
                    [Title]=@Title,[OpportunityId]=@OpportunityId,
                    [DueDate]=@DueDate,[TypeId]=@TypeId,[StatusId]=@StatusId
                  WHERE [Id]=@Id", task);

            if (rows == 0)
                throw new Exception("Item no longer available");

            OnAfterTaskUpdated(task);

            return task;
        }

        partial void OnTaskDeleted(CRMBlazorServerRBS.Models.RadzenCRM.Task item);
        partial void OnAfterTaskDeleted(CRMBlazorServerRBS.Models.RadzenCRM.Task item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.Task> DeleteTask(int id)
        {
            var item = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.Task>(
                "SELECT * FROM [dbo].[Tasks] WHERE [Id] = @Id", new { Id = id });

            if (item == null)
                throw new Exception("Item no longer available");

            OnTaskDeleted(item);

            await db.ExecuteAsync("DELETE FROM [dbo].[Tasks] WHERE [Id] = @Id", new { Id = id });

            OnAfterTaskDeleted(item);

            return item;
        }

        public async Task ExportTaskStatusesToExcel(Query query = null, string fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/radzencrm/taskstatuses/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/radzencrm/taskstatuses/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        public async Task ExportTaskStatusesToCSV(Query query = null, string fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/radzencrm/taskstatuses/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/radzencrm/taskstatuses/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        partial void OnTaskStatusesRead(ref IQueryable<CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus> items);

        public async Task<IQueryable<CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus>> GetTaskStatuses(Query query = null)
        {
            var statuses = await db.QueryAsync<CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus>(
                "SELECT * FROM [dbo].[TaskStatuses]");
            var items = statuses.AsQueryable();

            if (query != null)
            {
                if (!string.IsNullOrEmpty(query.Filter))
                {
                    items = query.FilterParameters != null
                        ? items.Where(query.Filter, query.FilterParameters)
                        : items.Where(query.Filter);
                }

                if (!string.IsNullOrEmpty(query.OrderBy))
                    items = items.OrderBy(query.OrderBy);

                if (query.Skip.HasValue)
                    items = items.Skip(query.Skip.Value);

                if (query.Top.HasValue)
                    items = items.Take(query.Top.Value);
            }

            OnTaskStatusesRead(ref items);

            return await Task.FromResult(items);
        }

        partial void OnTaskStatusGet(CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus> GetTaskStatusById(int id)
        {
            var item = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus>(
                "SELECT * FROM [dbo].[TaskStatuses] WHERE [Id] = @Id", new { Id = id });

            OnTaskStatusGet(item);

            return item;
        }

        partial void OnTaskStatusCreated(CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus item);
        partial void OnAfterTaskStatusCreated(CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus> CreateTaskStatus(CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus taskstatus)
        {
            OnTaskStatusCreated(taskstatus);

            var existing = await db.QueryFirstOrDefaultAsync<int?>(
                "SELECT [Id] FROM [dbo].[TaskStatuses] WHERE [Id] = @Id", new { taskstatus.Id });

            if (existing != null)
                throw new Exception("Item already available");

            taskstatus.Id = await db.QuerySingleAsync<int>(
                @"INSERT INTO [dbo].[TaskStatuses] ([Name]) VALUES (@Name);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);", taskstatus);

            OnAfterTaskStatusCreated(taskstatus);

            return taskstatus;
        }

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus> CancelTaskStatusChanges(CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus item)
        {
            return await GetTaskStatusById(item.Id);
        }

        partial void OnTaskStatusUpdated(CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus item);
        partial void OnAfterTaskStatusUpdated(CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus> UpdateTaskStatus(int id, CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus taskstatus)
        {
            OnTaskStatusUpdated(taskstatus);

            var rows = await db.ExecuteAsync(
                "UPDATE [dbo].[TaskStatuses] SET [Name]=@Name WHERE [Id]=@Id", taskstatus);

            if (rows == 0)
                throw new Exception("Item no longer available");

            OnAfterTaskStatusUpdated(taskstatus);

            return taskstatus;
        }

        partial void OnTaskStatusDeleted(CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus item);
        partial void OnAfterTaskStatusDeleted(CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus> DeleteTaskStatus(int id)
        {
            var item = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.TaskStatus>(
                "SELECT * FROM [dbo].[TaskStatuses] WHERE [Id] = @Id", new { Id = id });

            if (item == null)
                throw new Exception("Item no longer available");

            OnTaskStatusDeleted(item);

            await db.ExecuteAsync("DELETE FROM [dbo].[TaskStatuses] WHERE [Id] = @Id", new { Id = id });

            OnAfterTaskStatusDeleted(item);

            return item;
        }

        public async Task ExportTaskTypesToExcel(Query query = null, string fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/radzencrm/tasktypes/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/radzencrm/tasktypes/excel(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        public async Task ExportTaskTypesToCSV(Query query = null, string fileName = null)
        {
            navigationManager.NavigateTo(query != null ? query.ToUrl($"export/radzencrm/tasktypes/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')") : $"export/radzencrm/tasktypes/csv(fileName='{(!string.IsNullOrEmpty(fileName) ? UrlEncoder.Default.Encode(fileName) : "Export")}')", true);
        }

        partial void OnTaskTypesRead(ref IQueryable<CRMBlazorServerRBS.Models.RadzenCRM.TaskType> items);

        public async Task<IQueryable<CRMBlazorServerRBS.Models.RadzenCRM.TaskType>> GetTaskTypes(Query query = null)
        {
            var types = await db.QueryAsync<CRMBlazorServerRBS.Models.RadzenCRM.TaskType>(
                "SELECT * FROM [dbo].[TaskTypes]");
            var items = types.AsQueryable();

            if (query != null)
            {
                if (!string.IsNullOrEmpty(query.Filter))
                {
                    items = query.FilterParameters != null
                        ? items.Where(query.Filter, query.FilterParameters)
                        : items.Where(query.Filter);
                }

                if (!string.IsNullOrEmpty(query.OrderBy))
                    items = items.OrderBy(query.OrderBy);

                if (query.Skip.HasValue)
                    items = items.Skip(query.Skip.Value);

                if (query.Top.HasValue)
                    items = items.Take(query.Top.Value);
            }

            OnTaskTypesRead(ref items);

            return await Task.FromResult(items);
        }

        partial void OnTaskTypeGet(CRMBlazorServerRBS.Models.RadzenCRM.TaskType item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.TaskType> GetTaskTypeById(int id)
        {
            var item = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.TaskType>(
                "SELECT * FROM [dbo].[TaskTypes] WHERE [Id] = @Id", new { Id = id });

            OnTaskTypeGet(item);

            return item;
        }

        partial void OnTaskTypeCreated(CRMBlazorServerRBS.Models.RadzenCRM.TaskType item);
        partial void OnAfterTaskTypeCreated(CRMBlazorServerRBS.Models.RadzenCRM.TaskType item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.TaskType> CreateTaskType(CRMBlazorServerRBS.Models.RadzenCRM.TaskType tasktype)
        {
            OnTaskTypeCreated(tasktype);

            var existing = await db.QueryFirstOrDefaultAsync<int?>(
                "SELECT [Id] FROM [dbo].[TaskTypes] WHERE [Id] = @Id", new { tasktype.Id });

            if (existing != null)
                throw new Exception("Item already available");

            tasktype.Id = await db.QuerySingleAsync<int>(
                @"INSERT INTO [dbo].[TaskTypes] ([Name]) VALUES (@Name);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);", tasktype);

            OnAfterTaskTypeCreated(tasktype);

            return tasktype;
        }

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.TaskType> CancelTaskTypeChanges(CRMBlazorServerRBS.Models.RadzenCRM.TaskType item)
        {
            return await GetTaskTypeById(item.Id);
        }

        partial void OnTaskTypeUpdated(CRMBlazorServerRBS.Models.RadzenCRM.TaskType item);
        partial void OnAfterTaskTypeUpdated(CRMBlazorServerRBS.Models.RadzenCRM.TaskType item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.TaskType> UpdateTaskType(int id, CRMBlazorServerRBS.Models.RadzenCRM.TaskType tasktype)
        {
            OnTaskTypeUpdated(tasktype);

            var rows = await db.ExecuteAsync(
                "UPDATE [dbo].[TaskTypes] SET [Name]=@Name WHERE [Id]=@Id", tasktype);

            if (rows == 0)
                throw new Exception("Item no longer available");

            OnAfterTaskTypeUpdated(tasktype);

            return tasktype;
        }

        partial void OnTaskTypeDeleted(CRMBlazorServerRBS.Models.RadzenCRM.TaskType item);
        partial void OnAfterTaskTypeDeleted(CRMBlazorServerRBS.Models.RadzenCRM.TaskType item);

        public async Task<CRMBlazorServerRBS.Models.RadzenCRM.TaskType> DeleteTaskType(int id)
        {
            var item = await db.QueryFirstOrDefaultAsync<CRMBlazorServerRBS.Models.RadzenCRM.TaskType>(
                "SELECT * FROM [dbo].[TaskTypes] WHERE [Id] = @Id", new { Id = id });

            if (item == null)
                throw new Exception("Item no longer available");

            OnTaskTypeDeleted(item);

            await db.ExecuteAsync("DELETE FROM [dbo].[TaskTypes] WHERE [Id] = @Id", new { Id = id });

            OnAfterTaskTypeDeleted(item);

            return item;
        }
    }
}
