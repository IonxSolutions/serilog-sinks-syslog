// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using FakeItEasy;
using Serilog.Events;
using Xunit;
using static Serilog.Sinks.Syslog.Tests.Fixture;

namespace Serilog.Sinks.Syslog.Tests
{
    public class LocalSyslogSinkTests
    {
        /// <summary>
        /// We fake out the LocalSyslogService so we can test the sink on both Windows and Linux
        /// (the LocalSyslogService depends on the POSIX libc syslog functions)
        /// </summary>
        [Fact]
        public void Should_send_logs_to_syslog_service()
        {
            var syslogFormatter = new Rfc3164Formatter(Facility.Local0, "TestApp");
            var syslogService = A.Fake<LocalSyslogService>();
            var sink = new SyslogLocalSink(syslogFormatter, syslogService);

            var log = GetLogger(sink);

            log.Information("This is a test message");

            sink.Dispose();

            A.CallTo(() => syslogService.Open()).MustHaveHappened();
            A.CallTo(() => syslogService.WriteLog(134, A<string>.That.EndsWith("This is a test message"))).MustHaveHappened();
            A.CallTo(() => syslogService.Close()).MustHaveHappened();
        }

        [Fact]
        public void Should_override_severity_with_alternate_severityMapping()
        {
            const string msg = "The mapping of Serilog EventLogLevel.Verbose has been overridden to Syslog Severity.Debug.";

            var syslogFormatter = new Rfc3164Formatter(Facility.Local0, "TestApp",
                severityMapping: level => SyslogLoggerConfigurationExtensions.ValueBasedLogLevelToSeverityMap(level));

            var syslogService = A.Fake<LocalSyslogService>();
            var sink = new SyslogLocalSink(syslogFormatter, syslogService);

            var log = new LoggerConfiguration()
                .WriteTo.Sink(sink).MinimumLevel.Verbose()
                .CreateLogger();

            log.Verbose(msg);

            sink.Dispose();

            // With the custom severity mapping and a Facility of Local0, the calculated priority should be:
            // Local0 * 8 + Severity.Debug = 16 * 8 + 7 = 135
            A.CallTo(() => syslogService.Open()).MustHaveHappened();
            A.CallTo(() => syslogService.WriteLog(135, A<string>.That.EndsWith(msg))).MustHaveHappened();
            A.CallTo(() => syslogService.Close()).MustHaveHappened();
        }

        [Fact]
        public void Should_override_severity()
        {
            const string msg = "The mapping of Serilog EventLogLevel.Fatal has been overridden to Syslog Severity.Critical.";
            var syslogFormatter = new CustomSeverityMapping();
            var syslogService = A.Fake<LocalSyslogService>();
            var sink = new SyslogLocalSink(syslogFormatter, syslogService);

            var log = GetLogger(sink);

            log.Fatal(msg);

            sink.Dispose();

            A.CallTo(() => syslogService.Open()).MustHaveHappened();

            // With the custom severity mapping and a Facility of Local0, the calculated priority should be:
            // Local0 * 8 + Severity.Critical = 16 * 8 + 2 = 130
            A.CallTo(() => syslogService.WriteLog(130, A<string>.That.EndsWith(msg))).MustHaveHappened();
            A.CallTo(() => syslogService.Close()).MustHaveHappened();
        }

        private class CustomSeverityMapping : LocalFormatter
        {
            public CustomSeverityMapping() : base(Facility.Local0, null,
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
                    }
                )
            {

            }
        }
    }
}
