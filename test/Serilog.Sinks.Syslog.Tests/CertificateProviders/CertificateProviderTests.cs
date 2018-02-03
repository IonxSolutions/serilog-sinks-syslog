// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using Shouldly;
using static Serilog.Sinks.Syslog.Tests.Fixture;

namespace Serilog.Sinks.Syslog.Tests
{
    public class CertificateProviderTests
    {
        [Fact]
        public void Should_use_assigned_certificate()
        {
            var provider = new CertificateProvider(ClientCert);

            provider.Certificate.ShouldNotBeNull();
            provider.Certificate.Thumbprint.ShouldBe(ClientCertThumbprint, StringCompareShould.IgnoreCase);
        }

        [Fact]
        public void Should_throw_when_certificate_is_null()
        {
            Should.Throw<ArgumentNullException>(() =>
                new CertificateProvider(null));
        }

        [Fact]
        public void Should_throw_when_private_key_not_known()
        {
            Should.Throw<ArgumentException>(() =>
                new CertificateProvider(ClientCertWithoutKey));
        }
    }
}
