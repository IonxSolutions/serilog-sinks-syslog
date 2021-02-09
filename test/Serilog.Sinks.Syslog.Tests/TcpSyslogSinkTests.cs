// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Shouldly;
using static Serilog.Sinks.Syslog.Tests.Fixture;

namespace Serilog.Sinks.Syslog.Tests
{
    public class TcpSyslogSinkTests
    {
        private readonly List<string> messagesReceived = new List<string>();
        private X509Certificate2 clientCertificate;
        private X509Certificate2 serverCertificate;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly SyslogTcpConfig tcpConfig;
        private const SslProtocols SECURE_PROTOCOLS = SslProtocols.Tls11 | SslProtocols.Tls12;
        private readonly AsyncCountdownEvent countdown = new AsyncCountdownEvent(3);

        public TcpSyslogSinkTests()
        {
            this.tcpConfig = new SyslogTcpConfig
            {
                KeepAlive = true,
                Formatter = new Rfc5424Formatter(Facility.Local0, "TestApp"),
                Framer = new MessageFramer(FramingType.OCTET_COUNTING)
            };
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
            var logEvents = Some.LogEvents(3);
            await sink.EmitBatchAsync(logEvents);

            // Wait until the server has received all the messages we sent, or the timeout expires
            await this.countdown.WaitAsync(6000, this.cts.Token);

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
            var logEvents = Some.LogEvents(3);
            await sink.EmitBatchAsync(logEvents);

            // Wait until the server has received all the messages we sent, or the timeout expires
            await this.countdown.WaitAsync(6000, this.cts.Token);

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
    }
}
