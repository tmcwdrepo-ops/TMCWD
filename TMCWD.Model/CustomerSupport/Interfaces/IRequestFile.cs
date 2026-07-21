using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.CustomerSupport.Interfaces
{
    public interface IRequestFile
    {

        public int Id { get; set; }

        public int JobOrderId { get; set; }

        public RequestFileType RequestFileType { get; set; }

        public FileType Type { get; set; }

        public long Size { get; set; }

        public string Path { get; set; }

        public string PhysicalFilename { get; set; }

        public string OriginalFilename { get; set; }

        public int CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public int UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }

    }
}
