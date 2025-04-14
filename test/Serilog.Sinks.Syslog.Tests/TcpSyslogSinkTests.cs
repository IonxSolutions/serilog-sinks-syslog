// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Shouldly;
using static Serilog.Sinks.Syslog.Tests.Fixture;
using Xunit.Abstractions;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Parsing;

namespace Serilog.Sinks.Syslog.Tests
{
    [Collection("Uses Serilog SelfLog and Cannot be Run in Parallel")]
    public class TcpSyslogSinkTests : IDisposable
    {
        private readonly List<string> messagesReceived = new List<string>();
        private X509Certificate2 clientCertificate;
        private X509Certificate2 serverCertificate;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly SyslogTcpConfig tcpConfig;
        private readonly AsyncCountdownEvent countdown = new AsyncCountdownEvent(NumberOfEventsToSend);

        public TcpSyslogSinkTests(ITestOutputHelper output)
        {
            this.tcpConfig = new SyslogTcpConfig
            {
                KeepAlive = true,
                Formatter = new Rfc5424Formatter(Facility.Local0, "TestApp"),
                Framer = new MessageFramer(FramingType.OCTET_COUNTING)
            };

            // This can be useful to see any logging done by the sink. Especially when there is an exception
            // within the sink (and the sink has to catch and log it of course).
            // Ideally, we would configure this globally, or at least in an xUnit Class Fixture (https://xunit.net/docs/shared-context).
            // But the ITestOutputHelper output is not available there. So instead, this will get enabled
            // and disabled for each test, just in case it is needed.
            //
            // But also note that the Serilog Selflog is itself a global/static instance. With xUnit, unit
            // test classes are run in parallel. So we can't have multiple unit test classes enabling and
            // disabling the Serilog Selflog at the same time. So for any unit test classes that utilize
            // this, we will have to instruct xUnit to run them serially with the [Collection] attribute.
            Serilog.Debugging.SelfLog.Enable(x => { output.WriteLine(x); System.Diagnostics.Debug.WriteLine(x); });

            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        }

        public void Dispose()
        {
            Serilog.Debugging.SelfLog.Disable();
        }

        [Fact]
        public async Task Should_send_logs_to_tcp_syslog_service()
        {
            await SendUnsecureAsync(IPAddress.Loopback);
        }

        [Fact(Skip = "IPV6 is not yet available in the Travis or AppVeyor CI environments")]
        public async Task Should_send_logs_to_ipv6_tcp_syslog_service()
        {
            await SendUnsecureAsync(IPAddress.IPv6Loopback);
        }

        [Fact]
        public async Task Should_send_logs_to_secure_tcp_syslog_service_using_certificateSelectionCallback()
        {
            this.tcpConfig.KeepAlive = false; // Just to test the negative path
            this.tcpConfig.UseTls = true;
            this.tcpConfig.CertificateSelectionCallback = (sender, targetHost, localCertificates, remoteCertificates, acceptableIssuers) =>
            {
                // For our unit test purposes, we'll just use the CertificateProvider to return the test
                // certificate. However, your implementation of this callback method is free to do whatever
                // it wants. Most likely not using the built-in CertificateProvider.
                var certProvider = new CertificateProvider(ClientCert);

                return certProvider.Certificate;
            };
            this.tcpConfig.CertValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            {
                // So we know this callback was called
                this.serverCertificate = new X509Certificate2(certificate);
                return true;
            };

            // Start a simple TCP syslog server that will capture all received messages.
            var receiver = new TcpSyslogReceiver(ServerCert, this.cts.Token);
            receiver.MessageReceived += (_, msg) =>
            {
                this.messagesReceived.Add(msg);
                this.countdown.Signal();
            };

            // When a client connects, capture the client certificate they presented
            receiver.ClientAuthenticated += (_, cert) => this.clientCertificate = cert;

            this.tcpConfig.Host = IPAddress.Loopback.ToString();
            this.tcpConfig.Port = receiver.IPEndPoint.Port;

            var sink = new SyslogTcpSink(this.tcpConfig);

            // Generate and send 3 log events
            var logEvents = Some.LogEvents(NumberOfEventsToSend);
            await sink.EmitBatchAsync(logEvents);

            // Wait until the server has received all the messages we sent, or the timeout expires
            await this.countdown.WaitAsync(TimeoutInSeconds, this.cts.Token);

            // The server should have received all 3 messages sent by the sink
            this.messagesReceived.Count.ShouldBe(logEvents.Length);
            this.messagesReceived.ShouldAllBe(x => logEvents.Any(e => x.EndsWith(e.MessageTemplate.Text)));

            // The sink should have presented the client certificate to the server
            this.clientCertificate.Thumbprint
                .ShouldBe(ClientCert.Thumbprint, StringCompareShould.IgnoreCase);

            // The sink should have seen the server's certificate in the validation callback
            this.serverCertificate.Thumbprint
                .ShouldBe(ServerCert.Thumbprint, StringCompareShould.IgnoreCase);

            sink.Dispose();
            this.cts.Cancel();
        }

        [Fact]
        public async Task Should_send_logs_to_secure_tcp_syslog_service()
        {
            this.tcpConfig.KeepAlive = false; // Just to test the negative path
            this.tcpConfig.UseTls = true;
            this.tcpConfig.CertProvider = new CertificateProvider(ClientCert);
            this.tcpConfig.CertValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            {
                // So we know this callback was called
                this.serverCertificate = new X509Certificate2(certificate);
                return true;
            };

            // Start a simple TCP syslog server that will capture all received messages.
            var receiver = new TcpSyslogReceiver(ServerCert, this.cts.Token);
            receiver.MessageReceived += (_, msg) =>
            {
                this.messagesReceived.Add(msg);
                this.countdown.Signal();
            };

            // When a client connects, capture the client certificate they presented
            receiver.ClientAuthenticated += (_, cert) => this.clientCertificate = cert;

            this.tcpConfig.Host = IPAddress.Loopback.ToString();
            this.tcpConfig.Port = receiver.IPEndPoint.Port;

            var sink = new SyslogTcpSink(this.tcpConfig);

            // Generate and send 3 log events
            var logEvents = Some.LogEvents(NumberOfEventsToSend);
            await sink.EmitBatchAsync(logEvents);

            // Wait until the server has received all the messages we sent, or the timeout expires
            await this.countdown.WaitAsync(TimeoutInSeconds, this.cts.Token);

            // The server should have received all 3 messages sent by the sink
            this.messagesReceived.Count.ShouldBe(logEvents.Length);
            this.messagesReceived.ShouldAllBe(x => logEvents.Any(e => x.EndsWith(e.MessageTemplate.Text)));

            // The sink should have presented the client certificate to the server
            this.clientCertificate.Thumbprint
                .ShouldBe(ClientCert.Thumbprint, StringCompareShould.IgnoreCase);

            // The sink should have seen the server's certificate in the validation callback
            this.serverCertificate.Thumbprint
                .ShouldBe(ServerCert.Thumbprint, StringCompareShould.IgnoreCase);

            sink.Dispose();
            this.cts.Cancel();
        }

        [Fact]
        public void CertProvider_and_CertificateSelectionCallback_are_mutually_exclusive()
        {
            this.tcpConfig.UseTls = true;

            // Specifying both should throw an exception.
            this.tcpConfig.CertProvider = new CertificateProvider(ClientCert);
            this.tcpConfig.CertificateSelectionCallback = (sender, targetHost, localCertificates, remoteCertificates, acceptableIssuers) =>
            {
                // This method's implementation isn't important.
                var certProvider = new CertificateProvider(ClientCert);

                return certProvider.Certificate;
            };

            Assert.Throws<ArgumentException>(() => new SyslogTcpSink(this.tcpConfig));
        }

        [Fact]
        public async Task Extension_method_parameter_order()
        {
            // Start a simple TCP syslog server that will capture all received messages. This needs
            // to be SSL based, in order to specify all the parameters in the extension method.
            var receiver = new TcpSyslogReceiver(ServerCert, this.cts.Token);
            receiver.MessageReceived += (_, msg) =>
            {
                this.messagesReceived.Add(msg);
                this.countdown.Signal();
            };

            // When a client connects, capture the client certificate they presented
            receiver.ClientAuthenticated += (_, cert) => this.clientCertificate = cert;

            var logger = new LoggerConfiguration();

            // Specify every single parameter, implicitly, as the order cannot change between releases. While it
            // may be possible for the test to still pass if the parameters are changed, it's probably unlikely.
            logger.WriteTo.TcpSyslog(IPAddress.Loopback.ToString(),
                receiver.IPEndPoint.Port,
                "some application name",
                FramingType.OCTET_COUNTING,
                SyslogFormat.RFC5424,
                Facility.Local0, true,
                new CertificateProvider(ClientCert),
                (sender, certificate, chain, sslPolicyErrors) =>
                {
                    // So we know this callback was called
                    this.serverCertificate = new X509Certificate2(certificate);
                    return true;
                },
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                LogEventLevel.Debug,
                "SourceContext",
                SyslogLoggerConfigurationExtensions.DefaultBatchOptions,
                "source host",
                (logEventLevel) => Severity.Debug,
                new JsonFormatter(),
                levelSwitch: null,
                "meta",
                (LocalCertificateSelectionCallback)null)
                .MinimumLevel.Verbose();

            var log = logger.CreateLogger();

            var logEvents = Some.LogEvents(NumberOfEventsToSend);

            foreach (var item in logEvents)
            {
                log.Write(item);
            }

            // Wait until the server has received all the messages we sent, or the timeout expires
            await this.countdown.WaitAsync(TimeoutInSeconds, this.cts.Token);

            // The server should have received all 3 messages
            this.messagesReceived.Count.ShouldBe(logEvents.Length);

            // The sink should have presented the client certificate to the server
            this.clientCertificate.Thumbprint
                .ShouldBe(ClientCert.Thumbprint, StringCompareShould.IgnoreCase);

            // The sink should have seen the server's certificate in the validation callback
            this.serverCertificate.Thumbprint
                .ShouldBe(ServerCert.Thumbprint, StringCompareShould.IgnoreCase);

            this.cts.Cancel();
        }

        [Fact]
        public async Task Should_timeout_when_attempting_secure_tcp_to_non_secure_syslog_service()
        {
            // This is all that is needed for the client to attempt to initiate a TLS connection.
            this.tcpConfig.UseTls = true;

            // As for the server, note that we aren't passing in the server certificate and we're
            // instructing it to just listen and read and ignore any data.
            var receiver = new TcpSyslogReceiver(null, this.cts.Token, true);

            this.tcpConfig.Host = IPAddress.Loopback.ToString();
            this.tcpConfig.Port = receiver.IPEndPoint.Port;
            this.tcpConfig.TlsAuthenticationTimeout = TimeSpan.FromSeconds(TimeoutInSeconds);

            var sink = new SyslogTcpSink(this.tcpConfig);

            var logEvents = Some.LogEvents(NumberOfEventsToSend);

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await sink.EmitBatchAsync(logEvents));

            sink.Dispose();
            this.cts.Cancel();
        }

        private async Task SendUnsecureAsync(IPAddress address)
        {
            // Start a simple TCP syslog server that will capture all received messages.
            var receiver = new TcpSyslogReceiver(null, this.cts.Token);
            receiver.MessageReceived += (_, msg) =>
            {
                this.messagesReceived.Add(msg);
                this.countdown.Signal();
            };

            this.tcpConfig.Host = address.ToString();
            this.tcpConfig.Port = receiver.IPEndPoint.Port;

            var sink = new SyslogTcpSink(this.tcpConfig);

            // Generate and send 3 log events
            var logEvents = Some.LogEvents(NumberOfEventsToSend);
            await sink.EmitBatchAsync(logEvents);

            // Wait until the server has received all the messages we sent, or the timeout expires
            await this.countdown.WaitAsync(TimeoutInSeconds, this.cts.Token);

            // The server should have received all 3 messages sent by the sink
            this.messagesReceived.Count.ShouldBe(logEvents.Length);
            this.messagesReceived.ShouldAllBe(x => logEvents.Any(e => x.EndsWith(e.MessageTemplate.Text)));

            sink.Dispose();
            this.cts.Cancel();
        }

        // You can't set socket options *and* connect to an endpoint using a hostname - if
        // keep-alive is enabled, resolve the hostname to an IP
        // See https://github.com/dotnet/corefx/issues/26840
        [LinuxOnlyFact]
        public void Should_resolve_hostname_to_ip_on_linux_when_keepalive_enabled()
        {
            this.tcpConfig.Host = "localhost";

            var sink = new SyslogTcpSink(this.tcpConfig);
            sink.Host.ShouldBe("127.0.0.1");
        }

        [Fact]
        public async Task Extension_method_with_batchConfig()
        {
            var receiver = new TcpSyslogReceiver(null, this.cts.Token);
            receiver.MessageReceived += (_, msg) =>
            {
                this.messagesReceived.Add(msg);
                this.countdown.Signal();
            };

            // Change the defaults so we can recognize that they are taking effect.
            var batchConfig = new PeriodicBatching.PeriodicBatchingSinkOptions
            {
                EagerlyEmitFirstEvent = false,
                Period = TimeSpan.FromSeconds(TimeoutInSeconds),
            };

            var logger = new LoggerConfiguration().MinimumLevel.Debug();

            logger.WriteTo.TcpSyslog(IPAddress.Loopback.ToString(),
                receiver.IPEndPoint.Port,
                batchConfig: batchConfig);

            var log = logger.CreateLogger();

            // With the batching options set to not eagerly send the first events, we should be able to write
            // some events, wait, check the receiver to make sure we didn't receive any, then wait again.
            var logEvents = Some.LogEvents(NumberOfEventsToSend);

            foreach (var item in logEvents)
            {
                log.Write(item);
            }

            await this.countdown.WaitAsync(TimeSpan.FromSeconds(TimeoutInSeconds / 2f), this.cts.Token);

            this.messagesReceived.Count.ShouldBe(0);

            await this.countdown.WaitAsync(TimeoutInSeconds, this.cts.Token);

            this.messagesReceived.Count.ShouldBe(NumberOfEventsToSend);
            this.messagesReceived.ShouldAllBe(x => logEvents.Any(e => x.EndsWith(e.MessageTemplate.Text)));

            log.Dispose();
            this.cts.Cancel();
        }

        [Fact]
        public async Task Multiline_exception()
        {
            // Start a simple TCP syslog server that will capture all received messaged
            var receiver = new TcpSyslogReceiver(null, this.cts.Token);
            receiver.MessageReceived += (_, msg) =>
            {
                SelfLog.WriteLine(msg);
                this.messagesReceived.Add(msg);
                this.countdown.Signal();
            };

            var logger = new LoggerConfiguration();

            logger.WriteTo.TcpSyslog(IPAddress.Loopback.ToString(),
                receiver.IPEndPoint.Port,
                framingType: FramingType.OCTET_COUNTING,
                format: SyslogFormat.RFC5424,
                facility: Facility.Local0,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .MinimumLevel.Verbose();

            var log = logger.CreateLogger();

            // We will only be sending/testing a single event, but the countdown is expecting 3. Signal it
            // twice now so we don't have to wait for the timeout.
            this.countdown.Signal();
            this.countdown.Signal();

            try
            {
                DivideByZero();

                // We should not get here. A DivideByZeroException should be thrown, handled, and logged below.
            }
            catch (Exception ex)
            {
                var expectedText = new MessageTemplateParser().Parse("Test exception.");

                var le = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Fatal, ex, expectedText, Enumerable.Empty<LogEventProperty>());

                log.Write(le);
            }

            log.Dispose();

            // Wait until the server has received all the messages we sent, or the timeout expires
            await this.countdown.WaitAsync(TimeoutInSeconds, this.cts.Token);

            // The server should have received all 3 messages sent by the sink
            this.messagesReceived.Count.ShouldBe(1);
            this.messagesReceived.ShouldAllBe(x => x.Split('\r', '\n').Length > 1);

            this.cts.Cancel();
        }

        private static int DivideByZero()
        {
            // Just an additional method to be called, so as to increase the depth of the stack trace of the exception.
            var i = 0;
            var j = 42;
            var k = j / i;

            return k;
        }
    }
}
