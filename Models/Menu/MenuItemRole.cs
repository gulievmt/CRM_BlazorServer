namespace CRMBlazorServerRBS.Models.Menu
{
    public class MenuItemRole
    {
        public int MenuItemId { get; set; }
        public string RoleName { get; set; }
        public string Scope { get; set; } = "all";
        public string Permission { get; set; } = "read";
    }
}
