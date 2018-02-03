// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System.Net.Security;
using System.Security.Authentication;

namespace Serilog.Sinks.Syslog
{
    public class SyslogTcpConfig
    {
        /// <summary>
        /// Hostname/IP of the syslog server to connect to
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// TCP port the syslog server is listening on. Defaults to 1468, which is typically the
        /// default for non-TLS enabled syslog servers
        /// </summary>
        /// <remarks>
        /// For TLS-enabled syslog servers, 6514 is the IANA registered port
        /// </remarks>
        public int Port { get; set; } = 1468;

        /// <summary>
        /// Defines the syslog message format to be used
        /// </summary>
        public ISyslogFormatter Formatter { get; set; }

        /// <summary>
        /// Defines how syslog messages are framed. Defaults to the octet-counting method described in
        /// RFC5425 and RFC6587
        /// </summary>
        public MessageFramer Framer { get; set; } = new MessageFramer(FramingType.OCTET_COUNTING);

        /// <summary>
        /// Secure protocols to support. If None, the sink will connect to the server over an unsecure
        /// channel. Note that the server must support TLS in order to connect using a secure channel
        /// </summary>
        public SslProtocols SecureProtocols { get; set; } = SslProtocols.None;

        /// <summary>
        /// When SecureProtocols is not None, CertProvider can be used to present a client certificate
        /// to the syslog server. Leave as null if no client certificate is required
        /// </summary>
        public ICertificateProvider CertProvider { get; set; }

        /// <summary>
        /// Callback to validate the server's SSL certificate. If null, the system default will be used
        /// </summary>
        public RemoteCertificateValidationCallback CertValidationCallback { get; set; }
    }
}
