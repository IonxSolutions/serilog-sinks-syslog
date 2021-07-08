// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.Syslog.Tests
{
    /// <summary>
    /// Super-simple UDP syslog server implementation
    /// </summary>
    public class UdpSyslogReceiver
    {
        /// <summary>
        /// Register this event to be able to count or otherwise know and see
        /// the UDP packet messages that are received by this server</summary>
        public event EventHandler<string> MessageReceived;

        /// <summary>The address and port that the receiver is listening on</summary>
        public IPEndPoint ListeningIPEndPoint { get; private set; }

        private readonly UdpClient udpClient;
        private readonly CancellationToken cancellationToken;

        public UdpSyslogReceiver(CancellationToken ct)
        {
            // Enable listening on both IPv4 and IPv6.
            this.udpClient = new UdpClient(AddressFamily.InterNetworkV6);

            this.udpClient.Client.DualMode = true;

            this.udpClient.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));

            this.ListeningIPEndPoint = this.udpClient.Client.LocalEndPoint as IPEndPoint;

            this.cancellationToken = ct;

            this.cancellationToken.Register(() => this.udpClient.Close());

            // Similar to the TCP tests, in which the tests send 3 Syslog messages, the UDP tests
            // will do the same. But unlike TCP in which only one TCP connection was used to send
            // those 3 messages, UDP is obviously connection-less. So we need a way to setup a
            // receive "loop". We'll use the same pattern that is used in the TcpSyslogReceiver,
            // but just put it into a separate method so we can call it again in order to receive
            // the second and third messages.
            Receive();
        }

        private void Receive()
        {
            _ = this.udpClient.ReceiveAsync()
                .ContinueWith(ReceiveCallback, this.cancellationToken,
                    TaskContinuationOptions.NotOnFaulted | TaskContinuationOptions.NotOnCanceled,
                    TaskScheduler.Default)
                .ConfigureAwait(false);
        }

        private void ReceiveCallback(Task<UdpReceiveResult> task)
        {
            try
            {
                var message = Encoding.UTF8.GetString(task.Result.Buffer);

                MessageReceived?.Invoke(this, message);

                Receive();
            }
            catch (Exception ex)
            {
                // We shouldn't have to handle the typical OperationCanceledException you get with
                // async tasks because the .ContinueWith() is set to not call this callback method
                // when that happens. So we shouldn't expect any exceptions here, but just in case,
                // this may be helpful for future debugging.
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }
    }
}
