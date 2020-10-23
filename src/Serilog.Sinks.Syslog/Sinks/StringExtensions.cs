// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Text.RegularExpressions;

namespace Serilog.Sinks.Syslog
{
    public static class StringExtensions
    {
        private static readonly Regex printableAsciiRegex = new Regex("[^\\u0021-\\u007E]", RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// Truncates a string so that it is no longer than the specified number of characters.
        /// If the truncated string ends with a space, it will be removed
        /// </summary>
        /// <param name="source">String to be truncated</param>
        /// <param name="maxLength">Maximum string length before truncation will occur</param>
        /// <returns>Original string, or a truncated to the specified length if too long</returns>
        public static string WithMaxLength(this string source, int maxLength)
        {
            if (String.IsNullOrEmpty(source))
                return source;

            return source.Length > maxLength
                ? source.Substring(0, maxLength).TrimEnd()
                : source;
        }

        /// <summary>
        /// Remove any characters that are not in RFC5424's 'PRINTUSASCII'
        /// </summary>
        /// <remarks>
        /// The same character range is also specified in RFC3164, but it is not named
        /// </remarks>
        /// <param name="source">String to be processed</param>
        /// <returns>The string, with any characters in RFC5424's 'PRINTUSASCII' removed</returns>
        public static string AsPrintableAscii(this string source)
        {
            if (String.IsNullOrEmpty(source))
                return source;

            return printableAsciiRegex.Replace(source, String.Empty);
        }

        /// <summary>
        /// Remove any surrounding quotes, and unescape all others
        /// </summary>
        /// <param name="source">String to be processed</param>
        /// <returns>The string, with surrounding quotes removed and all others unescapes</returns>
        public static string TrimAndUnescapeQuotes(this string source)
        {
            if (String.IsNullOrEmpty(source))
                return source;

            return source
                .Trim('"')
                .Replace(@"\""", @"""");
        }

        public static int ToInt(this string source)
            => Convert.ToInt32(source);
    }
}
