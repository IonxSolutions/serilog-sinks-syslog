// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
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
        /// Enables the sending of keep-alive packets at the TCP socket layer. To be useful, keep-alive
        /// must be supported and enabled by the syslog server.
        /// </summary>
        /// <remarks>
        /// Defaults to false, as it's disabled by default on popular syslog servers such as rsyslog 
        /// and syslog-ng
        /// </remarks>
        public bool KeepAlive { get; set; } = false;

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
        
        /// <summary>
        /// When making a secure TCP connection, determines whether the server's certificate CRL, if
        /// specified in the CRL Distribution Point (CDP) extension of the certificate or any intermediate
        /// certificates, will be downloaded and checked for revocation. If any certificate within the
        /// certificate chain has been revoked, the connection will be aborted. That behavior, of course,
        /// can be customized by using the <see cref="CertValidationCallback"/> handler.
        /// </summary>
        public bool CheckCertificateRevocation { get; set; } = false;

        /// <summary>
        /// A timeout value for the TCP connection to perform the TLS handshake with the server. This is
        /// only applicable if <see cref="SecureProtocols"/> is set to a value other than the default,
        /// <see cref="SslProtocols.None"/>. This timeout value will ensure that if the server happens
        /// to not support TLS at all, for example, the connection may appear to hang, waiting for it to
        /// complete. This timeout will cause a disconnect and raise an exception after the elapsed time.
        /// The default value is 100 seconds.
        /// </summary>
        /// <remarks>This does not control the initial TCP connection timeout.</remarks>
        public TimeSpan TlsAuthenticationTimeout { get; set; } = TimeSpan.FromSeconds(100);
    }
}
