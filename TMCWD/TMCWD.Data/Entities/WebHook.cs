using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{

    [Table("webhooks")]
    public class WebHook
    {

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, MaxLength(255), Column("Data")]
        public string Data { get; set; } = string.Empty;

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }
 
    }
}
