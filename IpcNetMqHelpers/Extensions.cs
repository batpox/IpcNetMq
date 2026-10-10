using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IpcNetMq.IpcNetMqHelpers
{

    /// <summary>Provides numeric parsing and culture-invariant formatting extensions.</summary>
    public static class Extensions
    {

        /// <summary>Parses a floating-point value using the current culture.</summary>
        /// <param name="value">The numeric string to parse.</param>
        /// <returns>The parsed numeric value.</returns>
        public static double ToDouble(this string value)
        {
            return double.Parse(value);
        }

        /// <summary>Parses a floating-point value using invariant culture.</summary>
        /// <param name="value">The numeric string to parse.</param>
        /// <returns>The parsed numeric value.</returns>
        public static double ToDoubleInvariant(this string value)
        {
            return double.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Parses a 32-bit integer using the current culture.</summary>
        /// <param name="value">The numeric string to parse.</param>
        /// <returns>The parsed numeric value.</returns>
        public static Int32 ToInt32(this string value)
        {
            return Int32.Parse(value);
        }

        /// <summary>Parses a 32-bit integer using invariant culture.</summary>
        /// <param name="value">The numeric string to parse.</param>
        /// <returns>The parsed numeric value.</returns>
        public static Int32 ToInt32Invariant(this string value)
        {
            return Int32.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Converts an object to its invariant-culture string representation.</summary>
        /// <param name="obj">The object to convert, or null.</param>
        /// <returns>The converted string, or an empty string for null.</returns>
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

        /// <summary>Limits a string to the requested length.</summary>
        /// <param name="s">The string to truncate, or null.</param>
        /// <param name="maxLength">The maximum character count; a negative value leaves the string unchanged.</param>
        /// <returns>The truncated string, the original string when already short enough, or an empty string for null.</returns>
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
