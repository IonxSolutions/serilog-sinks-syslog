// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Shouldly;

namespace Serilog.Sinks.Syslog.Tests
{
    public class UdpSyslogSinkTests
    {
        private readonly List<string> messagesReceived = new List<string>();
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly AsyncCountdownEvent countdown = new AsyncCountdownEvent(3);

        [Fact(Skip="IPV6 is not yet available in the Travis or AppVeyor CI environments")]
        public async Task Should_send_logs_to_udp_syslog_service_ipv6()
            => await Should_send_logs_to_udp_syslog_service(GetFreeUdpEndPoint(true));

        [Fact]
        public async Task Should_send_logs_to_udp_syslog_service_ipv4()
            => await Should_send_logs_to_udp_syslog_service(GetFreeUdpEndPoint(false));

        private async Task Should_send_logs_to_udp_syslog_service(IPEndPoint endpoint)
        {
            var syslogFormatter = new Rfc3164Formatter(Facility.Local0, "TestApp");

            var sink = new SyslogUdpSink(endpoint, syslogFormatter);

            // Start a simple UDP syslog server that will capture all received messaged
            var receiver = new UdpSyslogReceiver();
            receiver.MessageReceived += (_, msg) =>
            {
                this.messagesReceived.Add(msg);
                this.countdown.Signal();
            };

            var receiveTask = receiver.StartReceiving(endpoint, this.cts.Token);

            // Generate and send 3 log events
            var logEvents = Some.LogEvents(3);
            await sink.EmitBatchAsync(logEvents);

            // Wait until the server has received all the messages we sent, or the timeout expires
            await this.countdown.WaitAsync(4000, this.cts.Token);

            // The server should have received all 3 messages sent by the sink
            this.messagesReceived.Count.ShouldBe(logEvents.Length);
            this.messagesReceived.ShouldAllBe(x => logEvents.Any(e => x.EndsWith(e.MessageTemplate.Text)));

            sink.Dispose();
            this.cts.Cancel();
            await receiveTask;
        }

        private static IPEndPoint GetFreeUdpEndPoint(bool asV6)
        {
            using var sock = new Socket(asV6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sock.Bind(new IPEndPoint(asV6 ? IPAddress.IPv6Loopback : IPAddress.Loopback, 0));

            return (IPEndPoint)sock.LocalEndPoint;
        }
    }
}
