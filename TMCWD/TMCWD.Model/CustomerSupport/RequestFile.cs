using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TMCWD.Model.CustomerSupport.Interfaces;

namespace TMCWD.Model.CustomerSupport
{
    public class RequestFile : IRequestFile
    {

        public RequestFile() { }

        [DisplayName("Id")]
        public int Id { get; set; }

        [DisplayName("Order Id")]
        public int JobOrderId { get; set; }
        [DisplayName("Request File Type")]
        public RequestFileType RequestFileType { get; set; }
        [DisplayName("File Type")]
        public FileType Type { get; set; }
        [DisplayName("Size")]
        public long Size { get; set; }
        [DisplayName("File Path")]
        public string Path { get; set; } = string.Empty;
        [DisplayName("Physical Filename")]
        public string PhysicalFilename { get; set; } = string.Empty;
        [DisplayName("Display Filename")]
        public string OriginalFilename { get; set; } = string.Empty;
        [DisplayName("Created By")]
        public int CreatedBy { get; set; }
        [DisplayName("Date Created")]
        public DateTime DateCreated { get; set; }
        [DisplayName("Updated By")]
        public int UpdatedBy { get; set; }
        [DisplayName("Date Updated")]
        public DateTime DateUpdated { get; set; }
    }
}
