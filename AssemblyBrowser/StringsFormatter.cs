// <copyright company="Dell Inc.">
//     Confidential and Proprietary
//     Copyright © 2015 Dell Inc. 
//     ALL RIGHTS RESERVED.
// </copyright>

using System.Globalization;
using System.Linq;

namespace AssemblyBrowser
{
    public static class StringsFormatter
    {
        public static string FormatPropertyName(string parent, string property)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}", parent, property);
        }

        public static string JoinWithUnderscore(params string[] objects)
        {
            return objects.Any() ? string.Join("_", objects) : string.Empty;
        }

        public static string JoinWithComma(params string[] objects)
        {
            return objects.Any() ? string.Join(",", objects) : string.Empty;
        }

        public static string JoinWithDot(params string[] objects)
        {
            return objects.Any() ? string.Join(".", objects) : string.Empty;
        }
        
        public static string SurroundWithBraces(string value)
        {
            return "{" + value + "}";
        }
    }
}