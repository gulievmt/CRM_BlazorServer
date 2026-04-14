using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRMBlazorServerRBS.Models.RadzenCRM
{
    [Table("Contacts", Schema = "dbo")]
    public partial class Contact
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Email { get; set; }

        public string Company { get; set; }

        public string LastName { get; set; }

        public string FirstName { get; set; }

        public string Phone { get; set; }

        /// <summary>
        /// SQL Server rowversion — автоматически обновляется при каждом изменении строки.
        /// Используется для оптимистичной блокировки (Optimistic Concurrency).
        /// </summary>
        [Column("row_version")]
        public byte[] RowVersion { get; set; }

        public ICollection<Opportunity> Opportunities { get; set; }

    }
}