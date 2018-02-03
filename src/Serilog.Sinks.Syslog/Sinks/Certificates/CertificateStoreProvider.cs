// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Serilog.Sinks.Syslog
{
    /// <inheritdoc />
    /// <summary>
    /// Provides a certificate for client authentication from the Certificate Store
    /// </summary>
    public class CertificateStoreProvider : ICertificateProvider
    {
        public X509Certificate2 Certificate { get; }

        public CertificateStoreProvider(StoreName storeName, StoreLocation storeLocation, string thumbprint)
        {
            using (var store = new X509Store(storeName, storeLocation))
            {
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                var foundCerts = store.Certificates
                    .Find(X509FindType.FindByThumbprint, thumbprint, false)
                    .OfType<X509Certificate2>()
                    .ToList();

                if (!foundCerts.Any())
                {
                    throw new ArgumentException($"Certificate {thumbprint} not found in {storeLocation}\\{storeName} store");
                }

                this.Certificate = foundCerts.FirstOrDefault();

                // You can't authenticate with a certificate unless you have the private key
                if (!this.Certificate.HasPrivateKey)
                    throw new ArgumentException("Certificate private key is not known");
            }
        }
    }
}
