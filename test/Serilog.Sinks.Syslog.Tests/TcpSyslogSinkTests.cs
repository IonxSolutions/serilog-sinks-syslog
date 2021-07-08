// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Shouldly;
using static Serilog.Sinks.Syslog.Tests.Fixture;
using Xunit.Abstractions;

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
        private const SslProtocols SECURE_PROTOCOLS = SslProtocols.Tls11 | SslProtocols.Tls12;
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
        public async Task Should_send_logs_to_secure_tcp_syslog_service()
        {
            this.tcpConfig.KeepAlive = false; // Just to test the negative path
            this.tcpConfig.SecureProtocols = SECURE_PROTOCOLS;
            this.tcpConfig.CertProvider = new CertificateProvider(ClientCert);
            this.tcpConfig.CertValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            {
                // So we know this callback was called
                this.serverCertificate = new X509Certificate2(certificate);
                return true;
            };

            // Start a simple TCP syslog server that will capture all received messaged
            var receiver = new TcpSyslogReceiver(ServerCert, SECURE_PROTOCOLS, this.cts.Token);
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
        public async Task Should_timeout_when_attempting_secure_tcp_to_non_secure_syslog_service()
        {
            // This is all that is needed for the client to attempt to initiate a TLS connection.
            this.tcpConfig.SecureProtocols = SECURE_PROTOCOLS;

            // As for the server, note that we aren't passing in the server certificate and we're
            // instructing it to just listen and read and ignore any data.
            var receiver = new TcpSyslogReceiver(null, SECURE_PROTOCOLS, this.cts.Token, true);

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
            // Start a simple TCP syslog server that will capture all received messaged
            var receiver = new TcpSyslogReceiver(null, SECURE_PROTOCOLS, this.cts.Token);
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
            var receiver = new TcpSyslogReceiver(null, SECURE_PROTOCOLS, this.cts.Token);
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
                secureProtocols: SslProtocols.None,
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
    }
}
