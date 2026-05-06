using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRMBlazorServerRBS.Models.Menu
{
    [Table("MenuItems", Schema = "dbo")]
    public class MenuItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(128)]
        public string Text { get; set; }

        [MaxLength(256)]
        public string Path { get; set; }        // null = group header (no navigation)

        [MaxLength(64)]
        public string Icon { get; set; }

        public int? ParentId { get; set; }      // null = top-level item

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;

        // Populated by MenuService — not stored in MenuItems table
        [NotMapped]
        public List<MenuItemRole> AllowedRoles { get; set; } = new();

        // Built in-memory when constructing the menu tree
        [NotMapped]
        public List<MenuItem> Children { get; set; } = new();

        [NotMapped]
        public bool HasChildren = false;

        [NotMapped]
        public string IconName => HasChildren ? "folder" : "article";

        public void CopyFrom(MenuItem src)
        {
            Text         = src.Text;
            Path         = src.Path;
            Icon         = src.Icon;
            ParentId     = src.ParentId;
            SortOrder    = src.SortOrder;
            IsActive     = src.IsActive;
            AllowedRoles = src.AllowedRoles;
            Children     = src.Children;
            HasChildren  = src.HasChildren;
        }
    }
}
