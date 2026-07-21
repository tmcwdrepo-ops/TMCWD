using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace TMCWD.Utility.Encryption
{
    public static class StringEncyption
    {

        public static string Encrypt(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] bInput = Encoding.UTF8.GetBytes(input);
                byte[] bHash = md5.ComputeHash(bInput);

                StringBuilder sb = new();
                for (int i = 0; i < bHash.Length; i++)
                {
                    sb.Append(bHash[i].ToString("x2"));
                }
                return sb.ToString();
            }

        }

    }
}
