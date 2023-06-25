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
        private readonly IPEndPoint ipEndPoint;
        private readonly CancellationToken cancellationToken;
        private readonly bool listenOnly;

        public event EventHandler<string> MessageReceived;
        public event EventHandler<X509Certificate2> ClientAuthenticated;

        public TcpSyslogReceiver(X509Certificate certificate,
             CancellationToken ct, bool listenOnly = false)
        {
            this.certificate = certificate;
            this.cancellationToken = ct;
            this.listenOnly = listenOnly;

            // In order to listen on both IPv4 and IPv6, if available, we must either specify IPv6Any for
            // the address and then manually set the DualMode property, or use the static TcpListener.Create()
            // method. The static method will automatically use IPv6Any and set the DualMode property of the
            // underlying socket to true for us. Then, by specifying zero for the port, a random available
            // port will also be acquired. We must call the Start method, however, before obtaining the IP
            // end point information in order to know what port is being used.
            this.tcpListener = TcpListener.Create(0);

            this.tcpListener.Start();

            this.ipEndPoint = this.tcpListener.Server.LocalEndPoint as IPEndPoint;

            ct.Register(() => this.tcpListener.Stop());

            _ = this.tcpListener.AcceptTcpClientAsync()
                .ContinueWith(HandleTcpConnection, ct,
                    TaskContinuationOptions.NotOnFaulted | TaskContinuationOptions.NotOnCanceled,
                    TaskScheduler.Default)
                .ConfigureAwait(false);
        }

        private async Task HandleTcpConnection(Task<TcpClient> task)
        {
            using (var tcpClient = task.Result)
            {
                tcpClient.NoDelay = true;

                Stream stream = tcpClient.GetStream();

                if (this.certificate != null)
                {
                    var sslStream = new SslStream(stream, false, ClientCertValidationCallback);
                    stream = sslStream;

                    try
                    {
                        await sslStream.AuthenticateAsServerAsync(this.certificate, true,
                            SslProtocols.None, false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        throw;
                    }
                }

                if (this.listenOnly)
                {
                    await ListenOnlyAsync(stream);
                }

                while (!this.cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Get the number of bytes in the message
                        var len = stream.ReadLength();

                        // Read the message
                        var messageBytes = await stream.ReadBytes(len, this.cancellationToken);
                        var message = Encoding.UTF8.GetString(messageBytes);

                        MessageReceived?.Invoke(this, message);
                    }
                    catch (EndOfStreamException)
                    {
                        // Client disconnected
                        break;
                    }
                }
            }
        }

        public IPEndPoint IPEndPoint => this.ipEndPoint;

        private bool ClientCertValidationCallback(object sender, X509Certificate cert,
            X509Chain chain, SslPolicyErrors policyErrors)
        {
            if (cert != null)
            {
                ClientAuthenticated?.Invoke(this, new X509Certificate2(cert));
            }

            return true;
        }

        private async Task ListenOnlyAsync(Stream stream)
        {
            while (!this.cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var buffer = new byte[4096];

                    _ = await stream.ReadAsync(buffer, 0, buffer.Length, this.cancellationToken);
                }
                catch
                {
                    // Ignore and try reading again until the cancellation token is signaled,
                    // telling us to stop.
                }
            }
        }
    }
}
