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
using Serilog.Parsing;
using Serilog.Debugging;

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
            const string propName = "WidgetProcess";
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port,
                format: SyslogFormat.RFC5424,
                messageIdPropertyName: propName);

            var evtProperties = new List<LogEventProperty>
            {
                new LogEventProperty(propName, new ScalarValue("Widget42")),
            };

            // Should produce log events like:
            // <134>1 2013-12-19T00:01:00.000000-07:00 DSGCH0FP72 testhost.net462.x86 2396 Widget42 [meta WidgetProcess="Widget42"] __2
            var logEvents = Some.LogEvents(NumberOfEventsToSend, evtProperties);

            await TestLoggerFromExtensionMethod(logger, receiver, altLogEvents: logEvents, altPropName: propName);
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

        [Fact]
        public async Task Extension_method_config_with_Rfc3164_format_and_severityMapping()
        {
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port,
                format: SyslogFormat.RFC3164,
                severityMapping: level =>
                level switch
                {
                    LogEventLevel.Verbose => Severity.Debug,
                    LogEventLevel.Debug => Severity.Debug,
                    LogEventLevel.Information => Severity.Informational,
                    LogEventLevel.Warning => Severity.Warning,
                    LogEventLevel.Error => Severity.Error,
                    LogEventLevel.Fatal => Severity.Critical,
                    _ => throw new ArgumentOutOfRangeException(nameof(level), $"The value {level} is not a valid LogEventLevel.")
                });

            // Should produce log events with a Syslog priority of 130. The default mapping would have produced 128.
            // Facility.Local0 * 8 + Severity.Critical = 16 * 8 + 2 = 130
            // <130>Dec 19 00:05:00 DSGCH0FP72 testhost.net462.x86[26964]: __6
            var logEvents = Some.LogEvents(NumberOfEventsToSend, LogEventLevel.Fatal);

            await TestLoggerFromExtensionMethod(logger, receiver, altLogEvents: logEvents, altPriority: 130);
        }

        [Fact]
        public async Task Extension_method_config_with_Rfc3164_format_and_alternate_severityMapping()
        {
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port,
                format: SyslogFormat.RFC3164,
                severityMapping: level => SyslogLoggerConfigurationExtensions.ValueBasedLogLevelToSeverityMap(level));

            // Should produce log events with a Syslog priority of 133. The default mapping would have produced 134.
            // Facility.Local0 * 8 + Severity.Notice = 16 * 8 + 5 = 133
            // <130>Dec 19 00:05:00 DSGCH0FP72 testhost.net462.x86[26964]: __6
            var logEvents = Some.LogEvents(NumberOfEventsToSend, LogEventLevel.Information);

            await TestLoggerFromExtensionMethod(logger, receiver, altLogEvents: logEvents, altPriority: 133);
        }

        [Fact]
        public async Task Extension_method_config_with_Rfc3164_format_and_verbose_log_level()
        {
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port,
                format: SyslogFormat.RFC3164).MinimumLevel.Verbose();

            // The mapping of Verbose happens to fall under the default case and gets mapped to Syslog's Severity.Notice.
            // Facility.Local0 * 8 + Severity.Notice = 16 * 8 + 5 = 133
            // <133>Dec 19 00:05:00 DSGCH0FP72 testhost.net462.x86[19572]: __6
            var logEvents = Some.LogEvents(NumberOfEventsToSend, LogEventLevel.Verbose);

            await TestLoggerFromExtensionMethod(logger, receiver, altLogEvents: logEvents, altPriority: 133);
        }

        [Fact]
        public async Task Extension_method_config_with_Rfc3164_format_and_debug_log_level()
        {
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port,
                format: SyslogFormat.RFC3164).MinimumLevel.Verbose();

            // The mapping of Debug gets mapped to Syslog's Severity.Debug.
            // Facility.Local0 * 8 + Severity.Debug = 16 * 8 + 7 = 135
            // <135>Dec 19 00:05:00 DSGCH0FP72 testhost.net462.x86[6728]: __6
            var logEvents = Some.LogEvents(NumberOfEventsToSend, LogEventLevel.Debug);

            await TestLoggerFromExtensionMethod(logger, receiver, altLogEvents: logEvents, altPriority: 135);
        }

        [Fact]
        public async Task Extension_method_config_with_Rfc3164_format_and_information_log_level()
        {
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port,
                format: SyslogFormat.RFC3164).MinimumLevel.Verbose();

            // The mapping of Information gets mapped to Syslog's Severity.Informational.
            // Facility.Local0 * 8 + Severity.Informational = 16 * 8 + 6 = 134
            // <134>Dec 19 00:05:00 DSGCH0FP72 testhost.net462.x86[6728]: __6
            var logEvents = Some.LogEvents(NumberOfEventsToSend, LogEventLevel.Information);

            await TestLoggerFromExtensionMethod(logger, receiver, altLogEvents: logEvents, altPriority: 134);
        }

        [Fact]
        public async Task Extension_method_config_with_Rfc3164_format_and_warning_log_level()
        {
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port,
                format: SyslogFormat.RFC3164).MinimumLevel.Verbose();

            // The mapping of Warning gets mapped to Syslog's Severity.Warning.
            // Facility.Local0 * 8 + Severity.Warning = 16 * 8 + 4 = 132
            // <132>Dec 19 00:05:00 DSGCH0FP72 testhost.net462.x86[6728]: __6
            var logEvents = Some.LogEvents(NumberOfEventsToSend, LogEventLevel.Warning);

            await TestLoggerFromExtensionMethod(logger, receiver, altLogEvents: logEvents, altPriority: 132);
        }

        [Fact]
        public async Task Extension_method_config_with_Rfc3164_format_and_error_log_level()
        {
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port,
                format: SyslogFormat.RFC3164).MinimumLevel.Verbose();

            // The mapping of Error gets mapped to Syslog's Severity.Error.
            // Facility.Local0 * 8 + Severity.Error = 16 * 8 + 3 = 131
            // <131>Dec 19 00:05:00 DSGCH0FP72 testhost.net462.x86[6728]: __6
            var logEvents = Some.LogEvents(NumberOfEventsToSend, LogEventLevel.Error);

            await TestLoggerFromExtensionMethod(logger, receiver, altLogEvents: logEvents, altPriority: 131);
        }

        [Fact]
        public async Task Extension_method_config_with_Rfc3164_format_and_fatal_log_level()
        {
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port,
                format: SyslogFormat.RFC3164).MinimumLevel.Verbose();

            // The mapping of Fatal gets mapped to Syslog's Severity.Emergency.
            // Facility.Local0 * 8 + Severity.Emergency = 16 * 8 + 0 = 128
            // <128>Dec 19 00:05:00 DSGCH0FP72 testhost.net462.x86[6728]: __6
            var logEvents = Some.LogEvents(NumberOfEventsToSend, LogEventLevel.Fatal);

            await TestLoggerFromExtensionMethod(logger, receiver, altLogEvents: logEvents, altPriority: 128);
        }

        [Fact]
        public async Task Extension_method_config_with_Rfc3164_format_and_fatal_log_level_exception()
        {
            var receiver = new UdpSyslogReceiver(this.cts.Token);

            var logger = new LoggerConfiguration();

            logger.WriteTo.UdpSyslog(IPAddress.Loopback.ToString(),
                receiver.ListeningIPEndPoint.Port,
                format: SyslogFormat.RFC3164,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .MinimumLevel.Verbose();

            // We will only be sending/testing a single event, but the countdown is expecting 3. Signal it
            // twice now so we don't have to wait for the timeout.
            this.countdown.Signal();
            this.countdown.Signal();

            try
            {
                DivideByZero();

                // We should not get here. A DivideByZeroException should be thrown, handled, and logged below.
            }
            catch (Exception ex)
            {
                // The TestLoggerFromExtensionMethod() method performs a check and expects this text to be matched
                // at the end of the string. Well, since we are doing an exception, the exception will be at the
                // end of the string, and we cannot possibly match it. So a bit of a hack is to just specify the
                // empty string, which will make it/allow it to successfully match anything.
                var expectedText = new MessageTemplateParser().Parse("");

                var le = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Fatal, ex, expectedText, Enumerable.Empty<LogEventProperty>());

                await TestLoggerFromExtensionMethod(logger, receiver, 1, new[] { le }, altPriority: 128);
            }
        }

        private static int DivideByZero()
        {
            // Just an additional method to be called, so as to increase the depth of the stack trace of the exception.
            var i = 0;
            var j = 42;
            var k = j / i;

            return k;
        }

        private async Task TestLoggerFromExtensionMethod(LoggerConfiguration logger, UdpSyslogReceiver receiver, int expected = NumberOfEventsToSend, LogEvent[] altLogEvents = null, string altPropName = null, int? altPriority = null)
        {
            receiver.MessageReceived += (_, msg) =>
            {
                this.messagesReceived.Add(msg);
                SelfLog.WriteLine(msg);
                this.countdown.Signal();
            };

            var log = logger.CreateLogger();

            var logEvents = altLogEvents ?? Some.LogEvents(expected);

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
                this.messagesReceived.Count.ShouldBe(expected);
                this.messagesReceived.ShouldAllBe(x => logEvents.Any(e => x.EndsWith(e.MessageTemplate.Text)));
            }

            if (altPropName != null)
            {
                this.messagesReceived.ShouldAllBe(x => logEvents.Any(e => x.Contains(altPropName)));
            }

            if (altPriority != null)
            {
                this.messagesReceived.ShouldAllBe(x => logEvents.Any(e => x.Contains(altPriority.ToString())));
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
