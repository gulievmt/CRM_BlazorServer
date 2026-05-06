using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRMBlazorServerRBS.Models.Menu;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CRMBlazorServerRBS.Services
{
    public class MenuService
    {
        private readonly string _connectionString;
        private readonly SecurityService _security;

        // Per-Blazor-circuit cache. Populated on first call, cleared after any admin write.
        private List<MenuItem> _cache;

        public MenuService(IConfiguration configuration, SecurityService security)
        {
            _connectionString = configuration.GetConnectionString("RadzenCRMConnection");
            _security = security;
        }

        private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

        // ── Public: filtered menu for the current user ─────────────────────

        public async Task<List<MenuItem>> GetMenuForCurrentUserAsync()
        {
            if (_cache != null)
                return _cache;

            using var db = CreateConnection();

            // 1. Load all active items in sort order
            var allItems = (await db.QueryAsync<MenuItem>(
                @"SELECT Id, Text, Path, Icon, ParentId, SortOrder, IsActive
                  FROM [dbo].[MenuItems]
                  WHERE IsActive = 1
                  ORDER BY SortOrder")).ToList();

            // 2. Load all role assignments in one query
            var allRoles = (await db.QueryAsync<MenuItemRole>(
                @"SELECT MenuItemId, RoleName, Scope, Permission FROM [dbo].[MenuItemRoles]")).ToList();

            // 3. Attach roles to items
            var rolesByItem = allRoles
                .GroupBy(r => r.MenuItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var item in allItems)
                item.AllowedRoles = rolesByItem.TryGetValue(item.Id, out var roles) ? roles : new List<MenuItemRole>();

            // 4. Filter: visible only when user is in at least one allowed role (empty = nobody)
            var visible = allItems
                .Where(i => i.AllowedRoles.Count > 0 && i.AllowedRoles.Any(r => _security.IsInRole(r.RoleName)))
                .ToList();

            // 5. Build tree: attach visible children to visible parents
            var result = new List<MenuItem>();

            foreach (var item in visible.Where(i => i.ParentId == null))
            {
                item.Children = visible
                    .Where(c => c.ParentId == item.Id)
                    .OrderBy(c => c.SortOrder)
                    .ToList();
                result.Add(item);
            }

            _cache = result.OrderBy(i => i.SortOrder).ToList();
            return _cache;
        }

        public void InvalidateCache() => _cache = null;

        // ── Admin: unfiltered flat list for the management grid ────────────

        public async Task<List<MenuItem>> GetAllMenuItemsFlatAsync()
        {
            using var db = CreateConnection();

            var items = (await db.QueryAsync<MenuItem>(
                @"SELECT Id, Text, Path, Icon, ParentId, SortOrder, IsActive
                  FROM [dbo].[MenuItems]
                  ORDER BY SortOrder")).ToList();

            var roles = (await db.QueryAsync<MenuItemRole>(
                @"SELECT MenuItemId, RoleName, Scope, Permission FROM [dbo].[MenuItemRoles]")).ToList();

            var rolesByItem = roles
                .GroupBy(r => r.MenuItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var item in items)
                item.AllowedRoles = rolesByItem.TryGetValue(item.Id, out var r) ? r : new List<MenuItemRole>();

            return items;
        }

        public async Task<MenuItem> GetMenuItemByIdAsync(int id)
        {
            using var db = CreateConnection();

            var item = await db.QueryFirstOrDefaultAsync<MenuItem>(
                @"SELECT Id, Text, Path, Icon, ParentId, SortOrder, IsActive
                  FROM [dbo].[MenuItems] WHERE Id = @Id", new { Id = id });

            if (item == null) return null;

            item.AllowedRoles = (await db.QueryAsync<MenuItemRole>(
                @"SELECT MenuItemId, RoleName, Scope, Permission
                  FROM [dbo].[MenuItemRoles] WHERE MenuItemId = @Id",
                new { Id = id })).ToList();

            return item;
        }

        // ── Admin CRUD ─────────────────────────────────────────────────────

        public async Task<int> CreateMenuItemAsync(MenuItemEditModel model)
        {
            using var db = CreateConnection();
            var newId = await db.QuerySingleAsync<int>(
                @"INSERT INTO [dbo].[MenuItems] (Text, Path, Icon, ParentId, SortOrder, IsActive)
                  VALUES (@Text, @Path, @Icon, @ParentId, @SortOrder, @IsActive);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new { model.Text, model.Path, model.Icon, model.ParentId, model.SortOrder, model.IsActive });

            await SyncRolesAsync(newId, model.SelectedRoles);
            InvalidateCache();
            return newId;
        }

        public async Task UpdateMenuItemAsync(MenuItemEditModel model)
        {
            using var db = CreateConnection();
            await db.ExecuteAsync(
                @"UPDATE [dbo].[MenuItems]
                  SET Text=@Text, Path=@Path, Icon=@Icon, ParentId=@ParentId,
                      SortOrder=@SortOrder, IsActive=@IsActive
                  WHERE Id=@Id",
                new { model.Text, model.Path, model.Icon, model.ParentId,
                      model.SortOrder, model.IsActive, model.Id });

            await SyncRolesAsync(model.Id, model.SelectedRoles);
            InvalidateCache();
        }

        public async Task DeleteMenuItemAsync(int id)
        {
            using var db = CreateConnection();
            // Orphan children rather than cascade-deleting them silently
            await db.ExecuteAsync(
                "UPDATE [dbo].[MenuItems] SET ParentId = NULL WHERE ParentId = @Id",
                new { Id = id });

            // MenuItemRoles rows cascade via FK ON DELETE CASCADE
            await db.ExecuteAsync(
                "DELETE FROM [dbo].[MenuItems] WHERE Id = @Id",
                new { Id = id });

            InvalidateCache();
        }

        // Replaces all role rows for one item
        private async Task SyncRolesAsync(int menuItemId, List<MenuItemRoleAssignment> selectedRoles)
        {
            using var db = CreateConnection();
            await db.ExecuteAsync(
                "DELETE FROM [dbo].[MenuItemRoles] WHERE MenuItemId = @MenuItemId",
                new { MenuItemId = menuItemId });

            if (selectedRoles?.Count > 0)
            {
                foreach (var r in selectedRoles)
                {
                    await db.ExecuteAsync(
                        @"INSERT INTO [dbo].[MenuItemRoles] (MenuItemId, RoleName, Scope, Permission)
                          VALUES (@MenuItemId, @RoleName, @Scope, @Permission)",
                        new { MenuItemId = menuItemId, r.RoleName, r.Scope, r.Permission });
                }
            }
        }
    }
}
