// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using Serilog.Events;
using Serilog.Parsing;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static Serilog.Sinks.Syslog.Tests.Fixture;

namespace Serilog.Sinks.Syslog.Tests
{
    public class SyslogRfc5424FormatterTests
    {
        private const string NILVALUE = "-";
        const string APP_NAME = "TestApp";
        const string SOURCE_CONTEXT = "TestCtx";
        private static readonly string Host = Environment.MachineName.WithMaxLength(255);

        private readonly ITestOutputHelper output;
        private readonly Rfc5424Formatter formatter = new Rfc5424Formatter(Facility.User, APP_NAME);
        private readonly DateTimeOffset timestamp;
        private readonly Regex regex;

        public SyslogRfc5424FormatterTests(ITestOutputHelper output)
        {
            this.output = output;

            // Prepare a regex object that can be used to check the output format
            // NOTE: The regex is in a text file instead of as a variable - it's a but large, and all the escaping required to
            // have it as a variable just makes it hard to grok
            var patternFilename = GetFullPath("Rfc5424Regex.txt");
            this.regex = new Regex(File.ReadAllText(patternFilename), RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

            // Timestamp used in tests
            var instant = new DateTime(2013, 12, 19, 4, 1, 2, 357) + TimeSpan.FromTicks(8523);
            this.timestamp = new DateTimeOffset(instant);
        }

        [Fact]
        public void Should_format_message_without_structured_data()
        {
            var template = new MessageTemplateParser().Parse("This is a test message");
            var infoEvent = new LogEvent(this.timestamp, LogEventLevel.Information, null, template, Enumerable.Empty<LogEventProperty>());

            var formatted = this.formatter.FormatMessage(infoEvent);
            this.output.WriteLine($"RFC5424 without structured data: {formatted}");

            var match = this.regex.Match(formatted);
            match.Success.ShouldBeTrue();

            match.Groups["pri"].Value.ShouldBe("<14>");
            match.Groups["ver"].Value.ShouldBe("1");
            match.Groups["timestamp"].Value.ShouldBe($"2013-12-19T04:01:02.357852{this.timestamp:zzz}");
            match.Groups["app"].Value.ShouldBe(APP_NAME);
            match.Groups["host"].Value.ShouldBe(Host);
            match.Groups["proc"].Value.ToInt().ShouldBeGreaterThan(0);
            match.Groups["msgid"].Value.ShouldBe(NILVALUE);
            match.Groups["sd"].Value.ShouldBe(NILVALUE);
            match.Groups["msg"].Value.ShouldBe("This is a test message");

            this.output.WriteLine($"FORMATTED: {formatted}");
        }

        [Fact]
        public void Should_format_message_with_structured_data()
        {
            const string testVal = "A Value";

            var properties = new List<LogEventProperty> {
                new LogEventProperty("AProperty", new ScalarValue(testVal)),
                new LogEventProperty("AnotherProperty", new ScalarValue("AnotherValue")),
                new LogEventProperty("SourceContext", new ScalarValue(SOURCE_CONTEXT))
            };

            var template = new MessageTemplateParser().Parse("This is a test message with val {AProperty}");
            var ex = new ArgumentException("Test");
            var warnEvent = new LogEvent(this.timestamp, LogEventLevel.Warning, ex, template, properties);

            var formatted = this.formatter.FormatMessage(warnEvent);
            this.output.WriteLine($"RFC5424 with structured data: {formatted}");

            var match = this.regex.Match(formatted);
            match.Success.ShouldBeTrue();

            match.Groups["pri"].Value.ShouldBe("<12>");
            match.Groups["ver"].Value.ShouldBe("1");
            match.Groups["timestamp"].Value.ShouldBe($"2013-12-19T04:01:02.357852{this.timestamp:zzz}");
            match.Groups["app"].Value.ShouldBe(APP_NAME);
            match.Groups["host"].Value.ShouldBe(Host);
            match.Groups["proc"].Value.ToInt().ShouldBeGreaterThan(0);
            match.Groups["msgid"].Value.ShouldBe(SOURCE_CONTEXT);
            match.Groups["sd"].Value.ShouldNotBe(NILVALUE);
            match.Groups["msg"].Value.ShouldBe($"This is a test message with val \"{testVal}\"");
        }

        [Fact]
        public void Should_choose_another_msgId_provider()
        {
            const string testProperty = "AProperty";
            const string testVal = "AValue";
            const string msgIdPropertyName = testProperty;
            var customFormatter = new Rfc5424Formatter(Facility.User, APP_NAME, null, null, msgIdPropertyName);

            var properties = new List<LogEventProperty>
            {
                new LogEventProperty(testProperty, new ScalarValue(testVal)),
                new LogEventProperty("AnotherProperty", new ScalarValue("AnotherValue")),
                new LogEventProperty("SourceContext", new ScalarValue(SOURCE_CONTEXT))
            };

            var template = new MessageTemplateParser().Parse("This is a test message with val {AProperty}");
            var ex = new ArgumentException("Test");
            var warnEvent = new LogEvent(this.timestamp, LogEventLevel.Warning, ex, template, properties);

            var formatted = customFormatter.FormatMessage(warnEvent);
            this.output.WriteLine($"RFC5424 with structured data: {formatted}");

            var match = this.regex.Match(formatted);
            match.Success.ShouldBeTrue();

            match.Groups["pri"].Value.ShouldBe("<12>");
            match.Groups["ver"].Value.ShouldBe("1");
            match.Groups["timestamp"].Value.ShouldBe($"2013-12-19T04:01:02.357852{this.timestamp:zzz}");
            match.Groups["app"].Value.ShouldBe(APP_NAME);
            match.Groups["host"].Value.ShouldBe(Host);
            match.Groups["proc"].Value.ToInt().ShouldBeGreaterThan(0);
            match.Groups["msgid"].Value.ShouldBe(testVal);
            match.Groups["sd"].Value.ShouldNotBe(NILVALUE);
            match.Groups["msg"].Value.ShouldBe($"This is a test message with val \"{testVal}\"");
        }

        /// <summary>
        /// RFC5424 rules:
        /// - Property names must be >= 1 and &lt;= 32 characters and may only contain printable ASCII
        ///   characters as defined by PRINTUSASCII
        ///
        /// - Property values must escape the characters '"', '\' and ']' with a backslash '\'
        ///
        /// - MSGID (source context) must be >= 1 and &lt;= 32 characters and may only contain printable ASCII
        ///   characters as defined by PRINTUSASCII
        /// </summary>
        [Fact]
        public void Should_clean_invalid_strings()
        {
            var properties = new List<LogEventProperty> {
                new LogEventProperty("安森Test", new ScalarValue(@"test")),
                new LogEventProperty("APropertyNameThatIsLongerThan32Characters", new ScalarValue(@"A value \contain]ing ""quotes"" to test")),
                new LogEventProperty("SourceContext", new ScalarValue("安森 A string that is longer than 32 characters"))
            };

            var template = new MessageTemplateParser().Parse("This is a test message");
            var infoEvent = new LogEvent(this.timestamp, LogEventLevel.Information, null, template, properties);

            var formatted = this.formatter.FormatMessage(infoEvent);
            this.output.WriteLine($"RFC5424: {formatted}");

            var match = this.regex.Match(formatted);
            match.Success.ShouldBeTrue();

            match.Groups["msgid"].Value.Length.ShouldBe(32);

            // Spaces and anything other than printable ASCII should have been removed
            match.Groups["msgid"].Value.ShouldStartWith("Astringthatis");

            // '"', '\' and ']' in property values should have been escaped with a backslash '\'
            Regex.IsMatch(match.Groups["sd"].Value, @"\\\\contain\\]ing\s\\""quotes\\""").ShouldBeTrue();

            // Property names have had spaces and anything other than printable ASCII should have been removed,
            // and should have been truncated to 32 chars
            Regex.IsMatch(match.Groups["sd"].Value, @"APropertyNameThatIsLongerThan32C="".*""\s").ShouldBeTrue();
        }
    }
}
