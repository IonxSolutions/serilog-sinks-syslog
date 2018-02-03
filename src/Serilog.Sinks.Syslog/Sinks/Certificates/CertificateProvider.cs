// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Security.Cryptography.X509Certificates;

namespace Serilog.Sinks.Syslog
{
    /// <inheritdoc />
    /// <summary>
    /// Provides a certificate for client authentication from a provided bundle
    /// </summary>
    public class CertificateProvider : ICertificateProvider
    {
        public X509Certificate2 Certificate { get; }

        public CertificateProvider(X509Certificate2 certificate)
        {
            if (certificate == null)
                throw new ArgumentNullException(nameof(certificate), "Certificate must be specified");

            // You can't authenticate with a certificate unless you have the private key
            if (!certificate.HasPrivateKey)
                throw new ArgumentException("Certificate private key is not known");

            this.Certificate = certificate;
        }
    }
}
