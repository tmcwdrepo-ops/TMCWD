using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TMCWD.Model.CustomerSupport.Interfaces;

namespace TMCWD.Model.CustomerSupport
{
    public class Material : IMaterial
    {

        public Material() { }

        [DisplayName("Id")]
        public int Id { get; set; }
        [DisplayName("Inventory Id")]
        public int InventoryId { get; set; }
        [DisplayName("Job Order Id")]
        public int JobOrderId { get; set; }
        [DisplayName("Requested Quantity")]
        public decimal RequestedQuantity { get; set; }
        [DisplayName("Selling Price")]
        public decimal UnitSellingPrice { get; set; }
        [DisplayName("Enrolled By")]
        public int CreatedBy { get; set; }
        [DisplayName("Enrolled Date")]
        public DateTime DateCreated { get; set; }
        [DisplayName("Updated By")]
        public int UpdatedBy { get; set; }
        [DisplayName("Date Updated")]
        public DateTime DateUpdated { get; set; }
        [DisplayName("Amount")]
        public decimal Amount => UnitSellingPrice * RequestedQuantity;
    }
}
