using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace IpcNetMq.IpcNetMqHelpers
{

    /// <summary>Provides endpoint naming helpers for inter-process coordination.</summary>
    public static class CommunicationHelpers
    {

        /// <summary>Hashes an endpoint into a name suitable for use as a mutex identifier.</summary>
        /// <remarks>The application may prepend Global\ to select the global mutex namespace.</remarks>
        /// <param name="serverAddress">The endpoint string to hash.</param>
        /// <param name="maxLength">The non-negative maximum length of the resulting identifier; defaults to 20.</param>
        /// <returns>A sanitized, truncated Base64 representation of the SHA-256 hash.</returns>
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
