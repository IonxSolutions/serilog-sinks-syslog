using System;
using System.Linq;
using System.Threading;
using Serilog.Events;
using Serilog.Parsing;

namespace Serilog.Sinks.Syslog.Tests
{
    internal static class Some
    {
        private static int counter;

        public static int Int() =>
            Interlocked.Increment(ref counter);

        public static string String(string tag = null) =>
            (tag ?? "") + "__" + Int();

        public static DateTimeOffset Instant() =>
            new DateTimeOffset(new DateTime(2013, 12, 19) + TimeSpan.FromMinutes(Int()));

        public static LogEvent LogEvent(LogEventLevel level = LogEventLevel.Information, string text = null)
            => new LogEvent(Instant(), level, null, MessageTemplate(text), Enumerable.Empty<LogEventProperty>());

        public static LogEvent[] LogEvents(int count, LogEventLevel level = LogEventLevel.Information, string text = null)
            => Enumerable.Range(0, count).Select(_ => LogEvent(level, text)).ToArray();

        public static MessageTemplate MessageTemplate(string text = null) =>
            new MessageTemplateParser().Parse(text ?? String());
    }
}
