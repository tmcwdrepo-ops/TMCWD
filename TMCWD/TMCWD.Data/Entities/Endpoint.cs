using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("endpoints")]
    public class Endpoint
    {

        #region constructors

        public Endpoint() { }

        #endregion

        #region properties

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, Column("GatewayType")]
        public int GatewayType { get; set; }

        [Required, Column("Name")]
        public string Name { get; set; } = string.Empty;

        [Required, Column("EndpointUrl")]
        public string EndpointUrl { get; set; } = string.Empty;

        #endregion

    }
}
