using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Interfaces
{
    public abstract class TransactionBase
    {

        protected string BaseUrl { get; set; } = "http://localhost:5178";

    }
}
