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
using Serilog.Sinks.Syslog.Tests.Support;

namespace Serilog.Sinks.Syslog.Tests
{
    /// <summary>
    /// Super-simple UDP syslog server implementation
    /// </summary>
    public class UdpSyslogReceiver
    {
        public event EventHandler<string> MessageReceived;

        public async Task StartReceiving(IPEndPoint listenEndpoint, CancellationToken ct)
        {
            using (var udpClient = new UdpClient(listenEndpoint))
            {
                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        var data = await udpClient.ReceiveAsync().WithCancellation(ct).ConfigureAwait(false);
                        var message = Encoding.UTF8.GetString(data.Buffer);

                        MessageReceived?.Invoke(this, message);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Stopping
                }
            }
        }
    }
}
