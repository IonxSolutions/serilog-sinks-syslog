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
using Xunit.Abstractions;
using Xunit.Sdk;

// Need to set the SystemDefaultTlsVersions before any tests run.
// See https://stackoverflow.com/a/53143426/8169136
[assembly: Xunit.TestFramework("Serilog.Sinks.Syslog.Tests.AssemblyFixture", "Serilog.Sinks.Syslog.Tests")]
namespace Serilog.Sinks.Syslog.Tests
{
    public sealed class AssemblyFixture : XunitTestFramework
    {
        public AssemblyFixture(IMessageSink messageSink) : base(messageSink)
        {
            TcpSyslogReceiver.SetAppContextDefaultForNet46TlsVersions();
        }
    }
}

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

            TcpSyslogReceiver.SetAppContextDefaultForNet46TlsVersions();
        }

        public static string ServerCertFilename => GetFullPath("server.p12");
        public static string ClientCertFilename => GetFullPath("client.p12");
        public static string ClientCertWithoutKeyFilename => GetFullPath("client.pem");
        public static X509Certificate2 ServerCert => LoadCertFromFile(ServerCertFilename);
        public static X509Certificate2 ClientCert => LoadCertFromFile(ClientCertFilename);
        public static X509Certificate2 ClientCertWithoutKey => LoadCertFromFile(ClientCertWithoutKeyFilename);
        public static string ClientCertThumbprint => "6c8ea2c439bf560e72a021f2d28264ca4ad0488b";

        public const int NumberOfEventsToSend = 3;
        public static readonly int TimeoutInSeconds = 3 * (int)SyslogLoggerConfigurationExtensions.DefaultBatchOptions.Period.TotalSeconds;

        public static ILogger GetLogger(ILogEventSink sink) => new LoggerConfiguration()
            .WriteTo.Sink(sink)
            .CreateLogger();

        public static string GetFullPath(string filename)
        {
            var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            return Path.Combine(baseDir, filename);
        }

        // Note, don't persist the storage of the private key because we don't go through the effort
        // to ever clean it up. And since the whole certificate is loaded from a file each time, there
        // is no need to persist it. If the private key is persisted, then every time the test is run,
        // a new file will be created in \Users\<user>\AppData\Roaming\Microsoft\Crypto\RSA\S-1-...
        // (when on Windows), and ultimately orphaned there. By not explicitly specifying to persist
        // the key, the private key will be written to the key store and remain until this variable
        // goes out of scope and is disposed. There is still a chance of orphaning and private key
        // file in the key store. Ideally, we'd like to use X509KeyStorageFlags.EphemeralKeySet, but
        // that option is not available on all .NET Framework versions being targeted.
        private static X509Certificate2 LoadCertFromFile(string filename)
            => new X509Certificate2(filename, String.Empty);
    }
}