// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

namespace Serilog.Sinks.Syslog
{
    /// <summary>
    /// Indicates the severity of a syslog message
    /// </summary>
    public enum Severity
    {
        /// <summary>
        /// System is unusable
        /// </summary>
        Emergency = 0,

        /// <summary>
        /// Action must be taken immediately
        /// </summary>
        Alert = 1,

        /// <summary>
        /// Critical conditions
        /// </summary>
        Critical = 2,

        /// <summary>
        /// Error conditions
        /// </summary>
        Error = 3,

        /// <summary>
        /// Warning conditions
        /// </summary>
        Warning = 4,

        /// <summary>
        /// Normal but significant condition
        /// </summary>
        Notice = 5,

        /// <summary>
        /// Informational messages
        /// </summary>
        Informational = 6,

        /// <summary>
        /// Debug-level messages
        /// </summary>
        Debug = 7
    }
}
