// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Shouldly;
using static Serilog.Sinks.Syslog.Tests.Fixture;

namespace Serilog.Sinks.Syslog.Tests
{
    public class UdpSyslogSinkTests
    {
        [Fact]
        public async Task Should_send_logs_to_udp_syslog_service()
        {
            var messagesReceived = new List<string>();
            var cts = new CancellationTokenSource();

            var syslogFormatter = new Rfc3164Formatter(Facility.Local0, "TestApp");
            var port = GetFreeUdpPort();
            var endpoint = new IPEndPoint(IPAddress.Loopback, port);
            var batchConfig = new BatchConfig(3, BatchConfig.Default.Period, 10);

            var sink = new SyslogUdpSink(endpoint, syslogFormatter, batchConfig);
            var log = GetLogger(sink);

            var receiver = new UdpSyslogReceiver();
            receiver.MessageReceived += (_, msg) => messagesReceived.Add(msg);
            var receiveTask = receiver.StartReceiving(endpoint, cts.Token);

            log.Information("This is test message 1");
            log.Warning("This is test message 2");
            log.Error("This is test message 3");

            await Task.Delay(200);

            messagesReceived.Count.ShouldBe(3);
            messagesReceived.ShouldContain(x => x.StartsWith("<134>"));
            messagesReceived.ShouldContain(x => x.StartsWith("<132>"));
            messagesReceived.ShouldContain(x => x.StartsWith("<131>"));

            sink.Dispose();
            cts.Cancel();
            await receiveTask;
        }

        private int GetFreeUdpPort()
        {
            using (var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                sock.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)sock.LocalEndPoint).Port;
            }
        }

        /// <summary>
        /// Super-simple UDP syslog server implementation
        /// </summary>
        private class UdpSyslogReceiver
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

                            this.MessageReceived?.Invoke(this, message);
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

    public static class AsyncExtensions
    {
        /// <summary>
        /// UdpClient doesn't support cancellation, so this wrapper adds it
        /// https://github.com/dotnet/corefx/issues/14308
        /// </summary>
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();

            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return task.Result;
        }
    }
}
