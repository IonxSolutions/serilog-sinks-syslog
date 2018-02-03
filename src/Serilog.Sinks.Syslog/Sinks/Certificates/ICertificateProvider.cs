// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System.Security.Cryptography.X509Certificates;

namespace Serilog.Sinks.Syslog
{
    /// <summary>
    /// Provider a certificate for client authentication
    /// </summary>
    /// <remarks>
    /// To be used for client authentication, the certificate private key must be known
    /// </remarks>
    public interface ICertificateProvider
    {
        X509Certificate2 Certificate { get; }
    }
}
