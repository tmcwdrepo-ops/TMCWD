using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.CustomerSupport.Interfaces
{
    public interface IMaterial
    {
        public int Id { get; set; }
        public int InventoryId { get; set; }
        public int JobOrderId { get; set; }
        public decimal RequestedQuantity { get; set; }
        public decimal UnitSellingPrice { get; set; }
        public int CreatedBy { get; set; }
        public DateTime DateCreated { get; set; }
        public int UpdatedBy { get; set; }
        public DateTime DateUpdated { get; set; }

    }
}
