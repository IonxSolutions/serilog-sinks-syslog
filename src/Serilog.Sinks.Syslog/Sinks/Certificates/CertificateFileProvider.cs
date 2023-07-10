// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Serilog.Sinks.Syslog
{
    /// <inheritdoc />
    /// <summary>
    /// Provides a certificate for client authentication from the filesystem
    /// </summary>
    public class CertificateFileProvider : ICertificateProvider
    {
        public X509Certificate2 Certificate { get; }

        public CertificateFileProvider(string filename, string password = null)
        {
            if (!File.Exists(filename))
                throw new FileNotFoundException($"Certificate {filename} could not be found");

            // Do not persist the private key. We do not have any code here to delete it. So the file ends up
            // being orphaned in the C:\Users\<user>\AppData\Roaming\Microsoft\Crypto\RSA\S-1-5-21-... folder.
            // Since we're not using this certificate in any HTTP connection or anything like that, we don't
            // need to persist it.
            this.Certificate = new X509Certificate2(filename, password);

            // You can't authenticate with a certificate unless you have the private key
            if (!this.Certificate.HasPrivateKey)
                throw new ArgumentException("Certificate private key is not known");
        }
    }
}
