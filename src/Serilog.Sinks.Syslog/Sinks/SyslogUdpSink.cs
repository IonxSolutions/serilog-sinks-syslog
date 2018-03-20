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
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.Syslog
{
    /// <summary>
    /// Sink that writes events to a remote syslog service using UDP
    /// </summary>
    public class SyslogUdpSink : PeriodicBatchingSink
    {
        private readonly ISyslogFormatter formatter;
        private UdpClient client;
        private readonly IPEndPoint endpoint;
        private bool disposed;

        public SyslogUdpSink(IPEndPoint endpoint, ISyslogFormatter formatter, BatchConfig batchConfig)
            : base(batchConfig.BatchSizeLimit, batchConfig.Period, batchConfig.QueueSizeLimit)
        {
            this.formatter = formatter;
            this.endpoint = endpoint;
            this.client = new UdpClient();
        }

        /// <summary>
        /// Emit a batch of log events, running asynchronously.
        /// </summary>
        /// <param name="events">The events to send to the syslog service</param>
        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
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
                    Console.WriteLine(ex);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                // If disposing == true, we're being called from an inheriting class calling base.Dispose()
                if (disposing)
                {
                    this.client.Dispose();
                    this.client = null;
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
