using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IpcNetMq.IpcNetMqHelpers
{
    public static class Extensions
    {
        public static double ToDouble(this string value)
        {
            return double.Parse(value);
        }
        public static double ToDoubleInvariant(this string value)
        {
            return double.Parse(value, CultureInfo.InvariantCulture);
        }
        public static Int32 ToInt32(this string value)
        {
            return Int32.Parse(value);
        }
        public static Int32 ToInt32Invariant(this string value)
        {
            return Int32.Parse(value, CultureInfo.InvariantCulture);
        }
        public static string ToStringInvariant(this object obj)
        {
            if ( obj == null) 
            {
                return string.Empty;
            }
            return Convert.ToString(obj, CultureInfo.InvariantCulture);
        }


    }

    /// <summary>
    /// String-specific extension methods.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Returns the string truncated to the specified maximum length.
        /// If the string was shorter than maxLength, the original string is returned.  
        /// </summary>
        /// <param name="s"></param>
        /// <param name="maxLength"></param>
        /// <returns></returns>
        public static string Trunc(this string s, int maxLength)
        {
            if (string.IsNullOrEmpty(s) || maxLength < 0)
                return s ?? string.Empty;

            return s.Length <= maxLength
                ? s
                : s.Substring(0, maxLength);
        }

        // Yeah, Someday with C# 8+ we can use ranges: :)
        ////public static string Truncate(this string s, int maxLength)
        ////{
        ////    if (string.IsNullOrEmpty(s))
        ////        return s ?? string.Empty;

        ////    return s.Length <= maxLength ? s : s[..maxLength];
        ////}
    }
}
