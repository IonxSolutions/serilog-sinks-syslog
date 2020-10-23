// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Diagnostics;
using System.IO;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Serilog.Sinks.Syslog
{
    /// <summary>
    /// Base class for formatters that output Serilog events in syslog formats
    /// </summary>
    /// <remarks>
    /// We purposely don't use Serilog's ITextFormatter to format syslog messages, so that users of this library
    /// can use their own ITextFormatter instances to control the format of the 'body' part of each message
    /// </remarks>
    public abstract class SyslogFormatterBase : ISyslogFormatter
    {
        private readonly Facility facility;
        private readonly MessageTemplateTextFormatter templateFormatter;
        protected static readonly string Host = Environment.MachineName.WithMaxLength(255);
        protected static readonly string ProcessId = Process.GetCurrentProcess().Id.ToString();
        protected static readonly string ProcessName = Process.GetCurrentProcess().ProcessName;

        protected SyslogFormatterBase(Facility facility, MessageTemplateTextFormatter templateFormatter)
        {
            this.facility = facility;
            this.templateFormatter = templateFormatter;
        }

        public abstract string FormatMessage(LogEvent logEvent);

        public int CalculatePriority(LogEventLevel level)
        {
            var severity = MapLogLevelToSeverity(level);
            return ((int)this.facility * 8) + (int)severity;
        }

        private static Severity MapLogLevelToSeverity(LogEventLevel logEventLevel)
            => logEventLevel switch
            {
                LogEventLevel.Debug => Severity.Debug,
                LogEventLevel.Error => Severity.Error,
                LogEventLevel.Fatal => Severity.Emergency,
                LogEventLevel.Information => Severity.Informational,
                LogEventLevel.Warning => Severity.Warning,
                _ => Severity.Notice
            };

        protected string RenderMessage(LogEvent logEvent)
        {
            if (this.templateFormatter != null)
            {
                using var sw = new StringWriter();

                this.templateFormatter.Format(logEvent, sw);
                return sw.ToString();
            }
            
            return logEvent.RenderMessage();
        }
    }
}
