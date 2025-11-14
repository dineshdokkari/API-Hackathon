using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace HackathonRepository.Helper
{
    public static class AesEncryptionHelper
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("utgswtgxiqlh2bgy");
        private static readonly byte[] Iv = Encoding.UTF8.GetBytes("ogtyhcguoklayhqn");

        public static string Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = Iv;
            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            var cipher = encryptor.TransformFinalBlock(Encoding.UTF8.GetBytes(plainText), 0, plainText.Length);
            return Convert.ToBase64String(cipher);
        }

        public static string Decrypt(string cipherText)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = Iv;
            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            var plainText = decryptor.TransformFinalBlock(Convert.FromBase64String(cipherText), 0, Convert.FromBase64String(cipherText).Length);
            return Encoding.UTF8.GetString(plainText);
        }
    }
}
