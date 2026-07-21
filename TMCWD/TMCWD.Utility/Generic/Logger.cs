using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model;
using System.IO;

namespace TMCWD.Utility.Generic
{
    public static class Logger
    {
        public static void Log(ErrorModule module, ErrorType type, string message)
        {
            string rootPath = "Logs";
            string filename = $"{DateTime.Now.ToString("d")}.txt";
            try
            {
                if (!Path.Exists(rootPath))
                {
                    Directory.CreateDirectory(rootPath);
                }
                using (TextWriter writer = new StreamWriter(Path.Combine(rootPath, filename)))
                {
                    writer.WriteLine("");
                    string text = $"{DateTime.Now.ToString("T")} - {module.ToString()} - {type.ToString()} - {message}";
                    writer.WriteLine(text);
                }
            }
            catch { }
        }
    }
}
