// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;

namespace Serilog.Sinks.Syslog
{
    [Flags]
    internal enum SyslogOptions
    {
        /// <summary>
        /// Include the process ID with each message
        /// </summary>
        LOG_PID = 1,

        /// <summary>
        /// Write directly to the system console if there is an error while sending to syslog
        /// </summary>
        LOG_CONS = 2,

        /// <summary>
        /// Delay opening of the connection until the first message is logged (default)
        /// </summary>
        LOG_ODELAY = 4,

        /// <summary>
        /// Open the connection immediately, instead of waiting until the first message is logged
        /// </summary>
        LOG_NDELAY = 8,

        /// <summary>
        /// No effect on Linux (deprecated)
        /// </summary>
        LOG_NOWAIT = 16,  // don't wait for console forks; DEPRECATED

        /// <summary>
        /// As well as sending to syslog, write to the caller's standard error stream
        /// </summary>
        LOG_PERROR = 32
    }
}
