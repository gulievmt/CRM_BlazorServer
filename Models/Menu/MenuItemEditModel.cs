using System.Collections.Generic;

namespace CRMBlazorServerRBS.Models.Menu
{
    /// <summary>
    /// Flat DTO used by the admin dialog form.
    /// Not mapped to any database table.
    /// </summary>
    public class MenuItemEditModel
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public string Path { get; set; }
        public string Icon { get; set; }
        public int? ParentId { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public List<string> SelectedRoles { get; set; } = new();
    }
}
