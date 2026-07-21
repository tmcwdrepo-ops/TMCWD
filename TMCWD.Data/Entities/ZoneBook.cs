using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("zones_books")]
    public class ZoneBook
    {

        #region constructors
        public ZoneBook() { }

        #endregion

        #region members

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, Column("Zone")]
        public int Zone { get; set; }

        [Required, Column("Book")]
        public int Book { get; set; }

        [Required, MaxLength(255), Column("Area")]
        public string Area { get; set; } = string.Empty;

        [Required, Column("Week")]
        public int Week { get; set; }

        #endregion

    }
}
