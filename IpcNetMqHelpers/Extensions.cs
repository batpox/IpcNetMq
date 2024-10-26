using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IpcNetMqHelpers
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
}
