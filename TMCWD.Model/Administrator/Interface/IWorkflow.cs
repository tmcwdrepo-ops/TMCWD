using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Administrator.Interface
{
    public interface IWorkflow
    {

        public int Id { get; set; }
        public string Name { get; set; }
        public int Sequence { get; set; }
        public string Predecessor { get; set; }
        public int CreatedBy { get; set; }
        public DateTime DateCreated { get; set; }
        public int UpdatedBy { get; set; }
        public DateTime DateUpdated { get; set; }
    }
}
