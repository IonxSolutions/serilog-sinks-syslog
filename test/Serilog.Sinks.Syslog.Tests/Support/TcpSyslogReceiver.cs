// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.Syslog.Tests
{
    /// <summary>
    /// Super-simple TCP syslog server implementation that only works with a single client
    /// </summary>
    public class TcpSyslogReceiver
    {
        private readonly TcpListener tcpListener;
        private readonly X509Certificate certificate;
        private readonly SslProtocols secureProtocols;

        public event EventHandler<string> MessageReceived;
        public event EventHandler<X509Certificate2> ClientAuthenticated;

        public TcpSyslogReceiver(IPEndPoint listenEndpoint, X509Certificate certificate,
            SslProtocols secureProtocols)
        {
            this.tcpListener = new TcpListener(listenEndpoint);
            this.certificate = certificate;
            this.secureProtocols = secureProtocols;
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
                        this.secureProtocols, false);
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

                    MessageReceived?.Invoke(this, message);
                }
                catch (EndOfStreamException)
                {
                    // Client disconnected
                    break;
                }
            }

            tcpClient.Dispose();
        });

        private bool ClientCertValidationCallback(object sender, X509Certificate cert,
            X509Chain chain, SslPolicyErrors policyErrors)
        {
            if (cert != null)
            {
                ClientAuthenticated?.Invoke(this, new X509Certificate2(cert.Export(X509ContentType.Pkcs12)));
            }

            return true;
        }
    }
}
