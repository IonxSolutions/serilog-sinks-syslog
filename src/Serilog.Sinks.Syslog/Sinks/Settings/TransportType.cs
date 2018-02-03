// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

namespace Serilog.Sinks.Syslog
{
    public enum TransportType
    {
        UDP = 1,

        TCP = 2,

        /// <summary>
        /// Secure TCP, using SSL/TLS over TCP
        /// </summary>
        TLS = 4
    }
}
