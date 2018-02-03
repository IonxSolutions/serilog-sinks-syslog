// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

namespace Serilog.Sinks.Syslog
{
    public enum SyslogFormat
    {
        /// <summary>
        /// The Linux libc syslog() method is only used to write the 'body' of the message - it
        /// takes care of the priority, timestamp etc by itself
        /// </summary>
        Local,

        /// <summary>
        /// Messages that comply with syslog RFC3164 https://tools.ietf.org/html/rfc3164
        /// </summary>
        RFC3164,

        /// <summary>
        /// Messages that comply with syslog RFC5424 https://tools.ietf.org/html/rfc5424
        /// </summary>
        RFC5424
    }
}
