using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TMCWD.Model.Engineering.Interfaces;

namespace TMCWD.Model.Engineering
{
    public class Inventory : IInventory
    {

        [DisplayName("Id")]
        public int Id { get; set; }
        [DisplayName("Item Name")]
        public string Name { get; set; } = string.Empty;
        [DisplayName("Division")]
        public int Division { get; set; }
        [DisplayName("Unit of Measure")]
        public string UOM { get; set; } = string.Empty;
        [DisplayName("Quantity")]
        public decimal Quantity { get; set; }
        [DisplayName("Unit Cost")]
        public decimal UnitCost { get; set; }
        [DisplayName("Selling Price")]
        public decimal UnitSellingPrice { get; set; }
        [DisplayName("Amout")]
        public decimal Amount { 
            get
            {
                return CalculateAmount();
            }
        }
        [DisplayName("Active")]
        public bool IsActive { get; set; }
        [DisplayName("Date Enrolled")]
        public DateTime DateCreated { get; set; }
        [DisplayName("Enrolled By")]
        public int CreatedBy { get; set; }
        [DisplayName("Date Updated")]
        public DateTime DateUpdated { get; set; }
        [DisplayName("Updated By")]
        public int UpdatedBy { get; set; }

        public decimal CalculateAmount()
        {
            return this.Quantity * this.UnitCost;
        }

    }
}
