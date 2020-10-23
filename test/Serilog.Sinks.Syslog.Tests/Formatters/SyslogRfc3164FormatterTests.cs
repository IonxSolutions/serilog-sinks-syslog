// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog.Events;
using Serilog.Parsing;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static Serilog.Sinks.Syslog.Tests.Fixture;

namespace Serilog.Sinks.Syslog.Tests
{
    public class SyslogRfc3164FormatterTests
    {
        const string APP_NAME = "TestAppWithAVeryLongNameThatShouldBeTooLong";
        private static readonly string Host = Environment.MachineName.WithMaxLength(255);

        private readonly ITestOutputHelper output;
        private readonly Rfc3164Formatter formatter = new Rfc3164Formatter(Facility.User, APP_NAME);
        private readonly DateTimeOffset timestamp;
        private readonly Regex regex;

        public SyslogRfc3164FormatterTests(ITestOutputHelper output)
        {
            this.output = output;

            // Prepare a regex object that can be used to check the output format
            // NOTE: The regex is in a text file instead of as a variable - it's a but large, and all the escaping required to
            // have it as a variable just makes it hard to grok
            var patternFilename = GetFullPath("Rfc3164Regex.txt");
            this.regex = new Regex(File.ReadAllText(patternFilename), RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

            // Timestamp used in tests
            var instant = new DateTime(2013, 12, 19, 4, 1, 2, 357) + TimeSpan.FromTicks(8523);
            this.timestamp = new DateTimeOffset(instant);
        }

        [Fact]
        public void Should_format_message_without_source_context()
        {
            var template = new MessageTemplateParser().Parse("This is a test message");
            var infoEvent = new LogEvent(this.timestamp, LogEventLevel.Information, null, template, Enumerable.Empty<LogEventProperty>());

            var formatted = this.formatter.FormatMessage(infoEvent);
            this.output.WriteLine($"RFC3164 without source context: {formatted}");

            var match = this.regex.Match(formatted);
            match.Success.ShouldBeTrue();

            match.Groups["pri"].Value.ShouldBe("<14>");
            match.Groups["timestamp"].Value.ShouldBe("Dec 19 04:01:02");
            match.Groups["app"].Value.ShouldBe("TestAppWithAVeryLongNameThatShou");
            match.Groups["host"].Value.ShouldBe(Host);
            match.Groups["proc"].Value.ToInt().ShouldBeGreaterThan(0);
            match.Groups["msg"].Value.ShouldBe("This is a test message");

            // Spaces and anything other than printable ASCII should have been removed, and should have
            // been truncated to 32 chars
            match.Groups["app"].Value.ShouldBe("TestAppWithAVeryLongNameThatShou");
        }

        [Fact]
        public void Should_format_message_with_source_context()
        {
            var properties = new List<LogEventProperty> {
                new LogEventProperty("SourceContext", new ScalarValue(@"Test.Cont""ext"))
            };

            var template = new MessageTemplateParser().Parse("This is a test message");
            var warnEvent = new LogEvent(this.timestamp, LogEventLevel.Warning, null, template, properties);

            var formatted = this.formatter.FormatMessage(warnEvent);
            this.output.WriteLine($"RFC3164 with source context: {formatted}");

            var match = this.regex.Match(formatted);
            match.Success.ShouldBeTrue();

            match.Groups["pri"].Value.ShouldBe("<12>");
            match.Groups["timestamp"].Value.ShouldBe("Dec 19 04:01:02");
            match.Groups["host"].Value.ShouldBe(Host);
            match.Groups["proc"].Value.ToInt().ShouldBeGreaterThan(0);

            // Spaces and anything other than printable ASCII should have been removed, and should have
            // been truncated to 32 chars
            match.Groups["app"].Value.ShouldBe("TestAppWithAVeryLongNameThatShou");

            // Ensure that we busted the source context out of its enclosing quotes, and that we unescaped
            // any other quotes
            match.Groups["msg"].Value.ShouldBe("[Test.Cont\"ext] This is a test message");
        }
    }
}
