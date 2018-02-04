// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Linq;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;
using Shouldly;

namespace Serilog.Sinks.Syslog.Tests
{
    public class LocalFormatterTests
    {
        private readonly LocalFormatter formatter = new LocalFormatter();

        [Fact]
        public void Should_format_message()
        {
            const string message = "This is a test message";
            var template = new MessageTemplateParser().Parse(message);
            var infoEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Information, null, template, Enumerable.Empty<LogEventProperty>());

            var formatted = this.formatter.FormatMessage(infoEvent);

            formatted.ShouldBe(message);
        }
    }
}
