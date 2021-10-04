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
        protected readonly Facility facility;

        /// <summary>See <see cref="Formatting.ITextFormatter"/>.</summary>
        protected readonly MessageTemplateTextFormatter templateFormatter;

        /// <summary><inheritdoc cref="SyslogLoggerConfigurationExtensions.LocalSyslog" path="/param[@name='severityMapping']"/></summary>
        protected readonly Func<LogEventLevel, Severity> severityMapping;

        /// <summary>Overrides the Hostname value in the syslog packet header. Max length 255. Defaults to <c>Environment.MachineName</c>.</summary>
        protected readonly string Host;
        protected static readonly string ProcessId = Process.GetCurrentProcess().Id.ToString();
        protected static readonly string ProcessName = Process.GetCurrentProcess().ProcessName;

        /// <summary>
        /// Common functionality for a derived syslog formatter class.
        /// </summary>
        /// <param name="facility"><inheritdoc cref="Facility" path="/summary"/></param>
        /// <param name="templateFormatter"><inheritdoc cref="templateFormatter" path="/summary"/></param>
        /// <param name="sourceHost"><inheritdoc cref="Host" path="/summary"/></param>
        /// <param name="severityMapping"><inheritdoc cref="SyslogLoggerConfigurationExtensions.LocalSyslog" path="/param[@name='severityMapping']"/></param>
        protected SyslogFormatterBase(
            Facility facility,
            MessageTemplateTextFormatter templateFormatter,
            string sourceHost = null,
            Func<LogEventLevel, Severity> severityMapping = null)
        {
            this.facility = facility;
            this.templateFormatter = templateFormatter;
            this.severityMapping = severityMapping ?? DefaultLogLevelToSeverityMap;

            // Use source hostname override, if specified
            this.Host = String.IsNullOrEmpty(sourceHost)
                ? Environment.MachineName.WithMaxLength(255)
                : sourceHost.WithMaxLength(255);
        }

        public abstract string FormatMessage(LogEvent logEvent);

        public virtual int CalculatePriority(LogEventLevel level)
        {
            var severity = this.severityMapping(level);
            return ((int)this.facility * 8) + (int)severity;
        }

        /// <summary>This is the original/default mapping between the available Serilog <see cref="LogEventLevel"/>s
        /// and those offered by the syslog <see cref="Severity"/>. Unfortunately, there is not a one-to-one mapping.
        /// So this method performs the mapping based on having most of the names between the two match.
        /// <para>One thing to note is that this mapping will cause the Serilog <see cref="LogEventLevel.Verbose"/>
        /// to be mapped to <see cref="Severity.Notice"/>, which doesn't match the typical relative importance
        /// between the two.</para></summary>
        /// <param name="logEventLevel">A Serilog <see cref="LogEventLevel"/>.</param>
        /// <returns>A syslog <see cref="Severity"/>.</returns>
        private static Severity DefaultLogLevelToSeverityMap(LogEventLevel logEventLevel)
            => logEventLevel switch
            {
                LogEventLevel.Debug => Severity.Debug,
                LogEventLevel.Information => Severity.Informational,
                LogEventLevel.Warning => Severity.Warning,
                LogEventLevel.Error => Severity.Error,
                LogEventLevel.Fatal => Severity.Emergency,
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
