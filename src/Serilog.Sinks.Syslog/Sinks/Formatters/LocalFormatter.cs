// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Serilog.Sinks.Syslog
{
    /// <inheritdoc />
    /// <summary>
    /// Formats messages for use with the Linux libc syslog() function. Note that syslog() is only
    /// used to write the 'body' of the message - it takes care of the priority, timestamp etc by
    /// itself, so this formatter is rather simple
    /// </summary>
    public class LocalFormatter : SyslogFormatterBase
    {
        /// <summary>
        /// Initialize a new instance of <see cref="LocalFormatter"/> class allowing you to specify values for
        /// the facility, application name and template formatter.
        /// </summary>
        /// <param name="facility"><inheritdoc cref="Facility" path="/summary"/></param>
        /// <param name="templateFormatter"><inheritdoc cref="SyslogFormatterBase.templateFormatter" path="/summary"/></param>
        /// <param name="severityMapping"><inheritdoc cref="SyslogLoggerConfigurationExtensions.LocalSyslog" path="/param[@name='severityMapping']"/></param>
        public LocalFormatter(Facility facility = Facility.Local0,
            MessageTemplateTextFormatter templateFormatter = null,
            Func<LogEventLevel, Severity> severityMapping = null)
            : base(facility, templateFormatter, severityMapping: severityMapping) { }

        public override string FormatMessage(LogEvent logEvent)
            => RenderMessage(logEvent);
    }
}
