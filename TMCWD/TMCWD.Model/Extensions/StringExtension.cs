using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Extensions
{
    public static class StringExtension
    {

        public static FileType GetFileTypeFromExtenstion(this string extension)
        {
            return extension.ToLower() switch
            {
                "png" => FileType.Png,
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
