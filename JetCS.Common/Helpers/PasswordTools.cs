using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.Common.Helpers
{
    public static class PasswordTools
    {
        static HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA512;
        const int keySize = 64;
        const int iterations = 340000;
        public static KeyValuePair<string,string> HashPassword(string password)
        {
            
            

            var salt = RandomNumberGenerator.GetBytes(keySize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                hashAlgorithm,
                keySize);
            return new KeyValuePair<string, string>(Convert.ToHexString(hash),Convert.ToHexString( salt) );
        }

        public static bool VerifyPassword(string password, string hash, string salt)
        {
            var saltBytes = Convert.FromHexString(salt);
            
            var hashToCompare = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, iterations, hashAlgorithm, keySize);

            return CryptographicOperations.FixedTimeEquals(hashToCompare, Convert.FromHexString(hash));
        }


    }
}
