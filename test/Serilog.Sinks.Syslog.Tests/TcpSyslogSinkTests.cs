// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
        private readonly BatchConfig batchConfig = new BatchConfig(3, BatchConfig.Default.Period, 10);
        private readonly IPEndPoint endpoint = GetFreeTcpEndPoint();
        private const SslProtocols SECURE_PROTOCOLS = SslProtocols.Tls11 | SslProtocols.Tls12;

        public TcpSyslogSinkTests()
        {
            this.tcpConfig = new SyslogTcpConfig
            {
                Host = "localhost",
                Port = this.endpoint.Port,
                Formatter = new Rfc5424Formatter(Facility.Local0, "TestApp"),
                Framer = new MessageFramer(FramingType.OCTET_COUNTING)
            };
        }

        [Fact]
        public async Task Should_send_logs_to_tcp_syslog_service()
        {
            var sink = new SyslogTcpSink(this.tcpConfig, this.batchConfig);
            var log = GetLogger(sink);

            var receiver = new TcpSyslogReceiver(this.endpoint, null);
            receiver.MessageReceived += (_, msg) => this.messagesReceived.Add(msg);
            var receiveTask = receiver.Start(this.cts.Token);

            log.Information("This is test message 1");
            log.Warning("This is test message 2");
            log.Error("This is test message 3");

            await Task.Delay(200);

            // The server should have received all 3 messages sent by the sink
            this.messagesReceived.Count.ShouldBe(3);
            this.messagesReceived.ShouldContain(x => x.StartsWith("<134>"));
            this.messagesReceived.ShouldContain(x => x.StartsWith("<132>"));
            this.messagesReceived.ShouldContain(x => x.StartsWith("<131>"));

            sink.Dispose();
            this.cts.Cancel();
            await receiveTask;
        }

        [Fact]
        public async Task Should_send_logs_to_secure_tcp_syslog_service()
        {
            this.tcpConfig.SecureProtocols = SECURE_PROTOCOLS;
            this.tcpConfig.CertProvider = new CertificateProvider(ClientCert);
            this.tcpConfig.CertValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            {
                // So we know this callback was called
                this.serverCertificate = new X509Certificate2(certificate);
                return true;
            };

            var sink = new SyslogTcpSink(this.tcpConfig, this.batchConfig);
            var log = GetLogger(sink);

            var receiver = new TcpSyslogReceiver(this.endpoint, ServerCert);
            receiver.MessageReceived += (_, msg) => this.messagesReceived.Add(msg);
            receiver.ClientAuthenticated += (_, cert) => this.clientCertificate = cert;

            var receiveTask = receiver.Start(this.cts.Token);

            log.Information("This is test message 1");
            log.Warning("This is test message 2");
            log.Error("This is test message 3");

            await Task.Delay(200);

            // The server should have received all 3 messages sent by the sink
            this.messagesReceived.Count.ShouldBe(3);
            this.messagesReceived.ShouldContain(x => x.StartsWith("<134>"));
            this.messagesReceived.ShouldContain(x => x.StartsWith("<132>"));
            this.messagesReceived.ShouldContain(x => x.StartsWith("<131>"));

            // The sink should have presented the client certificate to the server
            this.clientCertificate.Thumbprint
                .ShouldBe(ClientCert.Thumbprint, StringCompareShould.IgnoreCase);

            // The sink should have seen the server's certificate in the validation callback
            this.serverCertificate.Thumbprint
                .ShouldBe(ServerCert.Thumbprint, StringCompareShould.IgnoreCase);

            sink.Dispose();
            this.cts.Cancel();
            await receiveTask;
        }

        private static IPEndPoint GetFreeTcpEndPoint()
        {
            using (var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                sock.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                var port = ((IPEndPoint)sock.LocalEndPoint).Port;

                return new IPEndPoint(IPAddress.Loopback, port);
            }
        }

        /// <summary>
        /// Super-simple TCP syslog server implementation that only works with a single client
        /// </summary>
        private class TcpSyslogReceiver
        {
            private readonly TcpListener tcpListener;
            private readonly X509Certificate certificate;

            public event EventHandler<string> MessageReceived;
            public event EventHandler<X509Certificate2> ClientAuthenticated;

            public TcpSyslogReceiver(IPEndPoint listenEndpoint, X509Certificate certificate)
            {
                this.tcpListener = new TcpListener(listenEndpoint);
                this.certificate = certificate;
            }

            public Task Start(CancellationToken ct) => Task.Run(async () =>
            {
                this.tcpListener.Start();

                var tcpClient = await this.tcpListener.AcceptTcpClientAsync();
                tcpClient.NoDelay = true;
                tcpClient.ReceiveBufferSize = 32 * 1024;
                tcpClient.SendBufferSize = 4096;

                Stream stream = tcpClient.GetStream();

                if (this.certificate != null)
                {
                    var sslStream = new SslStream(stream, false, ClientCertValidationCallback);
                    stream = sslStream;

                    try
                    {
                        await sslStream.AuthenticateAsServerAsync(this.certificate, true,
                            SECURE_PROTOCOLS, false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        throw;
                    }
                }

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        // Get the number of bytes in the message
                        var len = stream.ReadLength();

                        // Read the message
                        var messageBytes = await stream.ReadBytes(len, ct);
                        var message = Encoding.UTF8.GetString(messageBytes);

                        this.MessageReceived?.Invoke(this, message);
                    }
                    catch (EndOfStreamException)
                    {
                        // Client disconnected
                        break;
                    }
                }

                tcpClient.Close();
            });

            private bool ClientCertValidationCallback(object sender, X509Certificate cert,
                X509Chain chain, SslPolicyErrors policyErrors)
            {
                if (cert != null)
                {
                    this.ClientAuthenticated?.Invoke(this, new X509Certificate2(cert));
                }

                return true;
            }
        }
    }
}
