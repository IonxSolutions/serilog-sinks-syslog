// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Linq;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;
using Xunit;
using Shouldly;

namespace Serilog.Sinks.Syslog.Tests
{
    public class SyslogFormatterBaseTests
    {
        private const string MESSAGE = "This is a test message";

        [Fact]
        public void Should_format_message_without_output_template()
        {
            var formatter = new TestFormatter();

            var template = new MessageTemplateParser().Parse(MESSAGE);
            var infoEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Information, null, template, Enumerable.Empty<LogEventProperty>());

            var formatted = formatter.FormatMessage(infoEvent);

            formatted.ShouldBe(MESSAGE);
        }

        [Fact]
        public void Should_format_message_with_output_template()
        {
            var templateFormatter = new MessageTemplateTextFormatter("--- {Message} ---", null);
            var formatter = new TestFormatter(templateFormatter);

            var template = new MessageTemplateParser().Parse(MESSAGE);
            var infoEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Information, null, template, Enumerable.Empty<LogEventProperty>());

            var formatted = formatter.FormatMessage(infoEvent);

            formatted.ShouldBe($"--- {MESSAGE} ---");
        }

        public class TestFormatter : SyslogFormatterBase
        {
            public TestFormatter(MessageTemplateTextFormatter templateFormatter = null)
                : base(Facility.Local0, templateFormatter) { }

            public override string FormatMessage(LogEvent logEvent)
            {
                return RenderMessage(logEvent);
            }
        }
    }
}
