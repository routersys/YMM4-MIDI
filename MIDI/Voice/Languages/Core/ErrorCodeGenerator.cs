using System;
using System.Security.Cryptography;
using System.Text;

namespace MIDI.Voice.Languages.Core
{
    public static class ErrorCodeGenerator
    {
        public static string Generate(char languagePrefix, string errorKey)
        {
            if (string.IsNullOrEmpty(errorKey))
            {
                return $"{languagePrefix}-000000";
            }

            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(errorKey));
                var sb = new StringBuilder();
                for (int i = 0; i < 3; i++)
                {
                    sb.Append(bytes[i].ToString("X2"));
                }
                string hashPrefix = sb.ToString().ToUpper();
                return $"{char.ToUpper(languagePrefix)}-{hashPrefix}";
            }
        }
    }
}