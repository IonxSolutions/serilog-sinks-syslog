// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.Syslog
{
    /// <summary>
    /// Sink that writes events to a remote syslog service using UDP
    /// </summary>
    public class SyslogUdpSink : IBatchedLogEventSink, IDisposable
    {
        private readonly ISyslogFormatter formatter;
        private UdpClient client;
        private readonly IPEndPoint endpoint;

        public SyslogUdpSink(IPEndPoint endpoint, ISyslogFormatter formatter)
        {
            this.formatter = formatter;
            this.endpoint = endpoint;
            this.client = new UdpClient(endpoint.AddressFamily);
        }

        /// <summary>
        /// Emit a batch of log events, running asynchronously.
        /// </summary>
        /// <param name="events">The events to send to the syslog service</param>
        public async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            foreach (var logEvent in events)
            {
                var message = this.formatter.FormatMessage(logEvent);
                var data = Encoding.UTF8.GetBytes(message);

                try
                {
                    await this.client.SendAsync(data, data.Length, this.endpoint).ConfigureAwait(false);
                }
                catch (SocketException ex)
                {
                    SelfLog.WriteLine($"[{nameof(SyslogTcpSink)}] error while sending log event to syslog {this.endpoint.Address}:{this.endpoint.Port} - {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        public Task OnEmptyBatchAsync()
            => Task.CompletedTask;

        public void Dispose()
        {
            this.client.Close();
            this.client.Dispose();
            this.client = null;
        }
    }
}
