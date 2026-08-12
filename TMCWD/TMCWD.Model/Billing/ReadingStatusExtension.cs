using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing
{
    public static class ReadingStatusExtension
    {
        public static string Description(this ReadingStatus status)
        {
            return status switch
            {
                ReadingStatus.Completed => "Completed",
                ReadingStatus.Deleted => "Deleted",
                ReadingStatus.InProgress => "In-Progress",
                _ => "None"
            };
        }
    }
}
