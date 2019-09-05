// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

namespace Serilog.Sinks.Syslog
{
    /// <summary>
    /// Methods used to delimit/frame individual syslog messages sent over TCP
    /// </summary>
    public enum FramingType
    {
        /// <summary>
        /// Carriage return, followed by line-feed (ASCII 13, 10)
        /// </summary>
        CRLF,

        /// <summary>
        /// Carriage return (ASCII 13)
        /// </summary>
        CR,

        /// <summary>
        /// Line-feed (ASCII 10)
        /// </summary>
        LF,

        /// <summary>
        /// NUL character (ASCII 00)
        /// </summary>
        NUL,

        /// <summary>
        /// The octet-counting method described in RFC5425 and RFC6587
        /// </summary>
        OCTET_COUNTING
    }
}
