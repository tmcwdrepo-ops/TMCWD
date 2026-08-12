using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Requests
{
    public class SaveTemplateRequest
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ReaderId { get; set; }
        public int Zone { get; set; }
        public int Book { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
