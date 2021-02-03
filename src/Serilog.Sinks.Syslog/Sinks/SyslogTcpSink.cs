// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.Syslog
{
    /// <summary>
    /// Sink that writes events to a remote syslog service over a TCP connection. Secured
    /// communication using TLS is support
    /// </summary>
    public class SyslogTcpSink : IBatchedLogEventSink, IDisposable
    {
        private TcpClient client;
        private Stream stream;
        private readonly ISyslogFormatter formatter;
        private readonly MessageFramer framer;
        private readonly bool enableKeepAlive;
        private readonly bool useTls;
        private readonly SslProtocols secureProtocols;
        private readonly X509Certificate2Collection clientCert;
        private readonly RemoteCertificateValidationCallback certValidationCallback;
        private readonly bool checkCertificateRevocation;

        public string Host { get; }
        public int Port { get; }

        public SyslogTcpSink(SyslogTcpConfig config)
        {
            this.formatter = config.Formatter;
            this.framer = config.Framer;
            this.Host = config.Host;
            this.Port = config.Port;
            this.enableKeepAlive = config.KeepAlive;

            this.secureProtocols = config.SecureProtocols;
            this.useTls = config.SecureProtocols != SslProtocols.None;
            this.certValidationCallback = config.CertValidationCallback;
            this.checkCertificateRevocation = config.CheckCertificateRevocation;

            if (config.CertProvider?.Certificate != null)
            {
                this.clientCert = new X509Certificate2Collection(new [] { config.CertProvider.Certificate });
            }

            // You can't set socket options *and* connect to an endpoint using a hostname - if
            // keep-alive is enabled, resolve the hostname to an IP
            // See https://github.com/dotnet/corefx/issues/26840
            if (config.KeepAlive && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (!IPAddress.TryParse(config.Host, out var addr))
                {
                    addr = Dns.GetHostAddresses(config.Host).First(x => x.AddressFamily == AddressFamily.InterNetwork);
                    this.Host = addr.ToString();
                }
            }
        }

        /// <summary>
        /// Emit a batch of log events, running asynchronously.
        /// </summary>
        /// <param name="events">The events to send to the syslog service</param>
        public async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            // Throws if not connected and unable to connect (PeriodicBatchingSink will handle retries)
            await EnsureConnected().ConfigureAwait(false);

            foreach (var logEvent in events)
            {
                var message = this.formatter.FormatMessage(logEvent);

                try
                {
                    await this.framer.WriteFrame(message, this.stream).ConfigureAwait(false);
                }
                catch (SocketException ex)
                {
                    // Log and rethrow (PeriodicBatchingSink will handle retries)
                    SelfLog.WriteLine($"[{nameof(SyslogTcpSink)}] error while sending log event to syslog {this.Host}:{this.Port} - {ex.Message}\n{ex.StackTrace}");
                    throw;
                }
            }
        }

        public Task OnEmptyBatchAsync()
            => Task.CompletedTask;

        protected async Task EnsureConnected()
        {
            try
            {
                if (IsConnected())
                    return;

                // Recreate the TCP client
                this.stream?.Dispose();
                this.client?.Close();

                // Allow connections to be made via IPv4 or IPv6. With just the default constructor,
                // only IPv4 can be used. To support both, we must specify the AddressFamily.InterNetworkV6
                // and set the DualMode property on the underlying socket to true. The DualMode property
                // can only be set to true when the AddressFamily is set to InterNetworkV6.
                //
                // But there is another caveat. If you call the .Connect() method overload that takes in a
                // DNS host name and port number, that method's code will first make a call to resolve the
                // DNS host name to an IP address (or addresses). It then validates that one of the resolved
                // IP addresses' AddressFamily type matches the AddressFamily type that was specified when
                // the TcpClient was constructed. If it doesn't find a match, for example, because we specify
                // AddressFamily.InterNetworkV6 and the DNS host name only resolves to an IPv4 address, it
                // will throw an error. Essentially, the .Connect() method that takes in a DNS host name and
                // port does not take into consideration the DualMode property that we set to true.
                //
                // All other .Connect() methods take in some form of IPAddress/IPEndPoint, which gets passed
                // directly down to the underlying socket, which is where the DualMode property exists.
                // Therefore, it is properly utilized and will work with either IPv4 or IPv6.
                //
                // So, we will make a call to resolve the DNS host name ourselves and call the .Connect()
                // method that takes in the array of IP addresses. That way, it will try each of them and will
                // work regardless of if the DNS host name resolved to an IPv4 or IPv6.
                this.client = new TcpClient(AddressFamily.InterNetworkV6);
                this.client.Client.DualMode = true;

                // If the Host name specified is already an IP address, then that is what will be returned.
                var hostAddresses = await Dns.GetHostAddressesAsync(this.Host).ConfigureAwait(false);

                // If we're running on Linux, only try to set keep-alives if they are wanted (in
                // that case we resolved the hostname to an IP in the ctor)
                // See https://github.com/dotnet/corefx/issues/26840
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || this.enableKeepAlive)
                {
                    this.client.Client.SetSocketOption(SocketOptionLevel.Socket,
                        SocketOptionName.KeepAlive, this.enableKeepAlive);
                }

                // Reduce latency to a minimum
                this.client.NoDelay = true;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Windows can support multiple connection attempts, thereby allowing us to pass in an
                    // array of addresses. See:
                    // https://github.com/dotnet/runtime/blob/release/5.0/src/libraries/System.Net.Sockets/src/System/Net/Sockets/Socket.cs#L5071
                    await this.client.ConnectAsync(hostAddresses, this.Port).ConfigureAwait(false);
                }
                else
                {
                    // However, multiple connection attempts is not guaranteed on other platforms. So we'll
                    // be cautious and only use the first IP address. If for whatever reason the caller was
                    // hoping that the second or some other IP address would be used, then they will just
                    // have to change their DNS so that the IP address they want will be resolved with the
                    // highest priority.
                    await this.client.ConnectAsync(hostAddresses.First(), this.Port).ConfigureAwait(false);
                }

                this.stream = await GetStream(this.client.GetStream()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Attempt to provide meaningful diagnostic messages for common connection problems
                HandleConnectError(ex);
                throw;
            }
        }

        private async Task<Stream> GetStream(Stream baseStream)
        {
            if (!this.useTls)
                return baseStream;

            // Authenticate the server, using the provided callback if required
            var sslStream = this.certValidationCallback != null
                ? new SslStream(baseStream, true, this.certValidationCallback)
                : new SslStream(baseStream, true);

            // Authenticate the client, using the provided client certificate if required
            // Note: this method takes an X509CertificateCollection, rather than an X509Certificate,
            // but providing the full chain does not actually appear to work for many servers
            await sslStream.AuthenticateAsClientAsync(this.Host, this.clientCert,
                this.secureProtocols, this.checkCertificateRevocation).ConfigureAwait(false);

            if (!sslStream.IsAuthenticated)
                throw new AuthenticationException("Unable to authenticate secure syslog server");

            return sslStream;
        }

        private bool IsConnected()
        {
            if (this.client?.Client == null || !this.client.Connected)
                return false;

            var socket = this.client.Client;

            // Poll will return true if there is no active connection OR if there is an active
            // connection and there is data waiting to be read
            return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
        }

        /// <summary>
        /// Attempt to provide meaningful diagnostic messages for common connection problems
        /// </summary>
        /// <param name="ex">The exception that occured during the failed connection attempt</param>
        private void HandleConnectError(Exception ex)
        {
            var prefix = $"[{nameof(SyslogTcpSink)}]";

            // Server down, blocked by a firewall, unreachable, or malfunctioning
            if (ex is SocketException socketEx)
            {
                var errorCode = socketEx.SocketErrorCode;

                if (errorCode == SocketError.ConnectionRefused)
                {
                    SelfLog.WriteLine($"{prefix} connection refused to {this.Host}:{this.Port} - is the server listening?");
                }
                else if (errorCode == SocketError.TimedOut)
                {
                    SelfLog.WriteLine($"{prefix} timed out connecting to {this.Host}:{this.Port} - is a firewall blocking traffic?");
                }
                else
                {
                    SelfLog.WriteLine($"{prefix} unable to connect to {this.Host}:{this.Port} - {ex.Message}\n{ex.StackTrace}");
                }
            }
            else if (ex is AuthenticationException)
            {
                // Issue with secure channel negotiation (e.g. protocol mismatch)
                var details = ex.InnerException?.Message ?? ex.Message;
                SelfLog.WriteLine($"{prefix} unable to connect to secure server {this.Host}:{this.Port} - {details}\n{ex.StackTrace}");
            }
            else
            {
                SelfLog.WriteLine($"{prefix} unable to connect to {this.Host}:{this.Port} - {ex.Message}\n{ex.StackTrace}");
            }

            // Tear down the client
            this.stream?.Dispose();
            this.client?.Close();
        }

        public void Dispose()
        {
            this.stream?.Dispose();
            this.stream = null;
            this.client?.Close();
            this.client = null;
        }
    }
}
