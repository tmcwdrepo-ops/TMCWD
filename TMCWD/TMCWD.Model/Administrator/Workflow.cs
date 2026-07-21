using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model.Administrator.Interface;

namespace TMCWD.Model.Administrator
{
    public class Workflow : IWorkflow
    {

        public Workflow() { }

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Sequence { get; set; }

        public string Predecessor { get; set; } = string.Empty;

        public int CreatedBy { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.Now;

        public int UpdatedBy { get; set; }
        public DateTime DateUpdated { get; set; }


    }
}
