// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Shouldly;
using static Serilog.Sinks.Syslog.Tests.Fixture;

namespace Serilog.Sinks.Syslog.Tests
{
    public class UdpSyslogSinkTests
    {
        private readonly List<string> messagesReceived = new List<string>();
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly BatchConfig batchConfig = new BatchConfig(3, BatchConfig.Default.Period, 10);
        private readonly IPEndPoint endpoint = GetFreeUdpEndPoint();
        private readonly AsyncCountdownEvent countdown = new AsyncCountdownEvent(3);

        [Fact]
        public async Task Should_send_logs_to_udp_syslog_service()
        {
            var syslogFormatter = new Rfc3164Formatter(Facility.Local0, "TestApp");

            var sink = new SyslogUdpSink(this.endpoint, syslogFormatter, this.batchConfig);
            var log = GetLogger(sink);

            var receiver = new UdpSyslogReceiver();

            receiver.MessageReceived += (_, msg) =>
            {
                this.messagesReceived.Add(msg);
                this.countdown.Signal();
            };

            var receiveTask = receiver.StartReceiving(this.endpoint, this.cts.Token);

            log.Information("This is test message 1");
            log.Warning("This is test message 2");
            log.Error("This is test message 3");

            await this.countdown.WaitAsync(20000, this.cts.Token);

            this.messagesReceived.Count.ShouldBe(3);
            this.messagesReceived.ShouldContain(x => x.StartsWith("<134>"));
            this.messagesReceived.ShouldContain(x => x.StartsWith("<132>"));
            this.messagesReceived.ShouldContain(x => x.StartsWith("<131>"));

            sink.Dispose();
            this.cts.Cancel();
            await receiveTask;
        }

        private static IPEndPoint GetFreeUdpEndPoint()
        {
            using (var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                sock.Bind(new IPEndPoint(IPAddress.Loopback, 0));

                return (IPEndPoint)sock.LocalEndPoint;
            }
        }
    }
}
