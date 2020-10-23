// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Security.Cryptography.X509Certificates;
using Shouldly;
using static Serilog.Sinks.Syslog.Tests.Fixture;

namespace Serilog.Sinks.Syslog.Tests
{
    public class CertificateStoreProviderTests : IDisposable
    {
        public CertificateStoreProviderTests()
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);

            store.Open(OpenFlags.ReadWrite | OpenFlags.OpenExistingOnly);
            store.Add(ClientCert);
        }

        [WindowsOnlyFact]
        public void Can_open_certificate_from_store()
        {
            var storeProvider = new CertificateStoreProvider(StoreName.My, StoreLocation.CurrentUser, ClientCertThumbprint);
            storeProvider.Certificate.ShouldNotBeNull();
            storeProvider.Certificate.Thumbprint.ShouldBe(ClientCertThumbprint, StringCompareShould.IgnoreCase);
        }

        [WindowsOnlyFact]
        public void Should_throw_when_no_certificate_with_thumbprint()
        {
            Should.Throw<ArgumentException>(() =>
                new CertificateStoreProvider(StoreName.My, StoreLocation.CurrentUser, "myergen"));
        }

        public void Dispose()
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            
            store.Open(OpenFlags.ReadWrite | OpenFlags.OpenExistingOnly);
            store.Remove(ClientCert);
        }
    }
}
