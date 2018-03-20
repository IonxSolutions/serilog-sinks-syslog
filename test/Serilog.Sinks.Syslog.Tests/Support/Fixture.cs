// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Serilog.Core;
using Shouldly;

namespace Serilog.Sinks.Syslog.Tests
{
    public static class Fixture
    {
        // Ensure support files exist
        static Fixture()
        {
            File.Exists(GetFullPath("Rfc3164Regex.txt")).ShouldBeTrue();
            File.Exists(GetFullPath("Rfc5424Regex.txt")).ShouldBeTrue();

            File.Exists(ClientCertFilename).ShouldBeTrue("If not present, certificates can be generated using /build/scripts/generate-certs.cmd");
            File.Exists(ServerCertFilename).ShouldBeTrue("If not present, certificates can be generated using /build/scripts/generate-certs.cmd");
        }

        public static string ServerCertFilename => GetFullPath("server.p12");
        public static string ClientCertFilename => GetFullPath("client.p12");
        public static string ClientCertWithoutKeyFilename => GetFullPath("client.pem");
        public static X509Certificate2 ServerCert => LoadCertFromFile(ServerCertFilename);
        public static X509Certificate2 ClientCert => LoadCertFromFile(ClientCertFilename);
        public static X509Certificate2 ClientCertWithoutKey => LoadCertFromFile(ClientCertWithoutKeyFilename);
        public static string ClientCertThumbprint => "6c8ea2c439bf560e72a021f2d28264ca4ad0488b";

        public static ILogger GetLogger(ILogEventSink sink) => new LoggerConfiguration()
            .WriteTo.Sink(sink)
            .CreateLogger();

        public static string GetFullPath(string filename)
        {
            var baseDir = Path.GetDirectoryName(typeof(Fixture).GetTypeInfo().Assembly.Location);

            return Path.Combine(baseDir, filename);
        }

        private static X509Certificate2 LoadCertFromFile(string filename)
        {
            return new X509Certificate2(filename, String.Empty, X509KeyStorageFlags.PersistKeySet);
        }
    }
}
