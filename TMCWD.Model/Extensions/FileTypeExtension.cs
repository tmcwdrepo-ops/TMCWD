using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace TMCWD.Model.Extensions
{
    public static class FileTypeExtension
    {

        public static FileType GetFromExtension(this FileType fileType, string extension)
        {
            return extension.ToLower() switch
            {
                "png" => FileType.Pdf,
                "jpg" => FileType.Jpeg,
                "img" => FileType.Image,
                "tiff" => FileType.Tiff,
                "pdf" => FileType.Pdf,
                "doc" => FileType.Word,
                "docx" => FileType.Word,
                "txt" => FileType.Text,
                _ => FileType.Other
            };
        }

    }
}
