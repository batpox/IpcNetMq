using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace IpcNetMq.IpcNetMqHelpers
{
    public static class CommunicationHelpers
    {
        /// <summary>
        /// Hash the server address into a legal mutex name of length not to exceed maxLength.
        /// </summary>
        /// <param name="serverAddress"></param>
        /// <param name="maxLength"></param>
        /// <returns></returns>
        public static string HashServerAddress(string serverAddress, int maxLength = 20)
        {
            // Use SHA256 to generate a hash of the server address
            using (SHA256 sha256 = SHA256.Create())
            {
                // Convert the input string into bytes
                byte[] addressBytes = Encoding.UTF8.GetBytes(serverAddress);

                // Compute the hash
                byte[] hashBytes = sha256.ComputeHash(addressBytes);

                // Convert hash to a base64 string to get a readable string
                string hashString = Convert.ToBase64String(hashBytes);
                // Replace invalid characters in a mutex name (remove slashes and replace +/= with valid chars)
                hashString = hashString.Replace("/", "")
                                       .Replace("\\", "")
                                       .Replace("+", "")
                                       .Replace("=", "");


                // Trim or take only the first N characters (default 20)
                return hashString.Substring(0, Math.Min(maxLength, hashString.Length));
            }
        }
    }
}
