// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

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
        /// <param name="facility">One of the <see cref="Facility"/> values indicating the machine process that created the syslog event. Defaults to <see cref="Facility.Local0"/>.</param>
        /// <param name="templateFormatter">See <see cref="Formatting.ITextFormatter"/>.</param>
        public LocalFormatter(Facility facility = Facility.Local0,
            MessageTemplateTextFormatter templateFormatter = null)
            : base(facility, templateFormatter) { }

        public override string FormatMessage(LogEvent logEvent)
            => RenderMessage(logEvent);
    }
}
