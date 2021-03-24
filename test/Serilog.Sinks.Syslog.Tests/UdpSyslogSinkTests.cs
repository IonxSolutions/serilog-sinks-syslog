// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Shouldly;
using Serilog.Events;
using Xunit.Abstractions;
using static Serilog.Sinks.Syslog.Tests.Fixture;

namespace Serilog.Sinks.Syslog.Tests
{
    [Collection("Uses Serilog SelfLog and Cannot be Run in Parallel")]
    public class UdpSyslogSinkTests : IDisposable
    {
        private readonly List<string> messagesReceived = new List<string>();
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly AsyncCountdownEvent countdown = new AsyncCountdownEvent(NumberOfEventsToSend);

        public UdpSyslogSinkTests(ITestOutputHelper output)
        {
            // This can be useful to see any logging done by the sink. Especially when there is an exception
            // within the sink (and the sink has to catch and log it of course).
            // Ideally, we would configure this globally, or at least in an xUnit Class Fixture (https://xunit.net/docs/shared-context).
            // But the ITestOutputHelper output is not available there. So instead, this will get enabled
            // and disabled for each test, just in case it is needed.
            //
            // But also note that the Serilog Selflog is itself a global/static instance. With xUnit, unit
            // test classes are run in parallel. So we can't have multiple unit test classes enabling and
            // disabling the Serilog Selflog at the same time. So for any unit test classes that utilize
            // this, we will have to instruct xUnit to run them serially with the [Collection] attribute.
            Serilog.Debugging.SelfLog.Enable(x => { output.WriteLine(x); System.Diagnostics.Debug.WriteLine(x); });
        }

        public void Dispose()
        {
            Serilog.Debugging.SelfLog.Disable();
        }

        [Fact(Skip="IPV6 is not yet available in the Travis or AppVeyor CI environments")]
        public async Task Should_send_logs_to_udp_syslog_service_ipv6()
            => await SendUdpAsync(IPAddress.IPv6Loopback);

        [Fact]
        public async Task Should_send_logs_to_udp_syslog_service_ipv4()
            => await SendUdpAsync(IPAddress.Loopback);

        [Fact]
        public async Task Extension_method_config_with_port()
        {
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration().MinimumLevel.Debug();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port);

            await TestLoggerFromExtensionMethod(logger, receiver);
        }

        [Fact]
        public async Task Extension_method_config_with_port_and_Rfc3164_format()
        {
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port,
                format: SyslogFormat.RFC3164);

            await TestLoggerFromExtensionMethod(logger, receiver);
        }

        [Fact]
        public async Task Extension_method_config_with_port_and_Rfc5424_format()
        {
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port,
                format: SyslogFormat.RFC5424);

            await TestLoggerFromExtensionMethod(logger, receiver);
        }

        [Fact]
        public async Task Extension_method_config_with_port_and_Rfc5424_format_and_messageIdPropertyName()
        {
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port,
                format: SyslogFormat.RFC5424,
                messageIdPropertyName: "WidgetProcess");

            var evtProperties = new List<LogEventProperty>
            {
                new LogEventProperty("WidgetProcess", new ScalarValue("Widget42")),
            };

            // Should produce log events like:
            // <134>1 2013-12-19T00:01:00.000000-07:00 DSGCH0FP72 testhost.net462.x86 2396 Widget42 [meta WidgetProcess="Widget42"] __2
            var logEvents = Some.LogEvents(NumberOfEventsToSend, evtProperties);

            await TestLoggerFromExtensionMethod(logger, receiver, altLogEvents: logEvents);
        }

        [Fact]
        public async Task Extension_method_config_with_port_and_restricted_minimum_level()
        {
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port,
                restrictedToMinimumLevel: LogEventLevel.Fatal);

            // In the logger configuration, the restricted minimum level is set to Fatal, which is the
            // highest value. But the log events that will be generated and sent to the log are only at
            // the Information level. Therefore, they will not be written/sent to the log and will be
            // ignored. We don't expect to receive any log events and the test will only complete when
            // the timeout has elapsed.
            await TestLoggerFromExtensionMethod(logger, receiver, 0);
        }

        private async Task TestLoggerFromExtensionMethod(LoggerConfiguration logger, UdpSyslogReceiver receiver, int expected = NumberOfEventsToSend, Events.LogEvent[] altLogEvents = null)
        {
            receiver.MessageReceived += (_, msg) =>
            {
                this.messagesReceived.Add(msg);
                this.countdown.Signal();
            };

            var log = logger.CreateLogger();

            var logEvents = altLogEvents ?? Some.LogEvents(NumberOfEventsToSend);

            foreach (var item in logEvents)
            {
                log.Write(item);
            }

            log.Dispose();

            // Wait until the server has received all the messages we sent, or the timeout expires
            await this.countdown.WaitAsync(TimeoutInSeconds, this.cts.Token);

            if (expected > 0)
            {
                // The server should have received all 3 messages sent by the sink
                this.messagesReceived.Count.ShouldBe(NumberOfEventsToSend);
                this.messagesReceived.ShouldAllBe(x => logEvents.Any(e => x.EndsWith(e.MessageTemplate.Text)));
            }

            if (altLogEvents != null)
            {
                var source = altLogEvents.First().Properties.Keys.First();

                this.messagesReceived.ShouldAllBe(x => logEvents.Any(e => x.Contains(source)));
            }

            this.cts.Cancel();
        }

        private async Task SendUdpAsync(IPAddress address)
        {
            // Start a simple UDP syslog server that will capture all received messaged
            var receiver = new UdpSyslogReceiver(this.cts.Token);
            receiver.MessageReceived += (_, msg) =>
            {
                this.messagesReceived.Add(msg);
                this.countdown.Signal();
            };

            var syslogFormatter = new Rfc3164Formatter(Facility.Local0, "TestApp");

            var ipEndPoint = new IPEndPoint(address, receiver.ListeningIPEndPoint.Port);

            var sink = new SyslogUdpSink(ipEndPoint, syslogFormatter);

            // Generate and send 3 log events
            var logEvents = Some.LogEvents(NumberOfEventsToSend);
            await sink.EmitBatchAsync(logEvents);

            // Wait until the server has received all the messages we sent, or the timeout expires
            await this.countdown.WaitAsync(TimeoutInSeconds, this.cts.Token);

            // The server should have received all 3 messages sent by the sink
            this.messagesReceived.Count.ShouldBe(logEvents.Length);
            this.messagesReceived.ShouldAllBe(x => logEvents.Any(e => x.EndsWith(e.MessageTemplate.Text)));

            sink.Dispose();
            this.cts.Cancel();
        }
    }
}
