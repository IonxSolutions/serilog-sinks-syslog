// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using FakeItEasy;
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
    }
}
