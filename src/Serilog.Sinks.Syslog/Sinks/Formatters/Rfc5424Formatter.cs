// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Serilog.Sinks.Syslog
{
    /// <inheritdoc />
    /// <summary>
    /// Formats messages that comply with syslog RFC5424
    /// https://tools.ietf.org/html/rfc5424
    /// </summary>
    public class Rfc5424Formatter : SyslogFormatterBase
    {
        /// <summary>
        /// Used in place of data that cannot be obtained or is unavailable
        /// </summary>
        private const string NILVALUE = "-";

        /// <summary>
        /// 'meta' is an IANA-assigned SD-ID that is used to provide meta-information
        /// about the message
        /// </summary>
        private const string STRUCTURED_DATA_ID = "meta";

        /// <summary>
        /// RFC5424 mandates the use of a timestamp that is a slightly more constrained version of that specified
        /// in RFC3339 (which is in turn based on that of ISO 8601)
        /// </summary>
        /// <remarks>
        /// </remarks>
        private const string DATE_FORMAT = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.ffffffzzz";

        private readonly string applicationName;
        private readonly string messageIdPropertyName;

        internal const string DefaultMessageIdPropertyName = "SourceContext";

        /// <summary>
        /// Initialize a new instance of <see cref="Rfc5424Formatter"/> class allowing you to specify values for
        /// the facility, application name, template formatter, and message Id property name.
        /// </summary>
        /// <param name="facility">One of the <see cref="Facility"/> values indicating the machine process that created the syslog event. Defaults to <see cref="Facility.Local0"/>.</param>
        /// <param name="applicationName">A user supplied value representing the application name that will appear in the syslog event. Must be all printable ASCII characters. Max length 48. Defaults to the current process name.</param>
        /// <param name="templateFormatter">See <see cref="Formatting.ITextFormatter"/>.</param>
        /// <param name="messageIdPropertyName">Where the Id number of the message will be derived from. Defaults to the "SourceContext" property of the syslog event. Property name and value must be all printable ASCII characters with max length of 32.</param>
        public Rfc5424Formatter(Facility facility = Facility.Local0, string applicationName = null,
            MessageTemplateTextFormatter templateFormatter = null,
            string messageIdPropertyName = DefaultMessageIdPropertyName)
            : base(facility, templateFormatter)
        {
            this.applicationName = applicationName ?? ProcessName;

            // Conform to the RFC
            this.applicationName = this.applicationName
                .AsPrintableAscii()
                .WithMaxLength(48);

            // Conform to the RFC
            this.messageIdPropertyName = (messageIdPropertyName ?? DefaultMessageIdPropertyName)
                                         .AsPrintableAscii()
                                         .WithMaxLength(32);
        }

        // NOTE: For the rsyslog daemon to correctly handle RFC5424, you need to change your /etc/rsyslog.conf to use:
        // $ActionFileDefaultTemplate RSYSLOG_SyslogProtocol23Format
        // instead of:
        // $ActionFileDefaultTemplate RSYSLOG_TraditionalFileFormat
        public override string FormatMessage(LogEvent logEvent)
        {
            var priority = CalculatePriority(logEvent.Level);
            var messageId = GetMessageId(logEvent);

            var timestamp = logEvent.Timestamp.ToString(DATE_FORMAT);
            var sd = RenderStructuredData(logEvent);
            var msg = RenderMessage(logEvent);

            return $"<{priority}>1 {timestamp} {Host} {this.applicationName} {ProcessId} {messageId} {sd} {msg}";
        }

        /// <summary>
        /// Get the LogEvent's SourceContext in a format suitable for use as the MSGID field of a syslog message
        /// </summary>
        /// <param name="logEvent">The LogEvent to extract the context from</param>
        /// <returns>The processed SourceContext, or NILVALUE '-' if not set</returns>
        private string GetMessageId(LogEvent logEvent)
        {
            var hasMsgId = logEvent.Properties.TryGetValue(this.messageIdPropertyName, out LogEventPropertyValue propertyValue);

            if (!hasMsgId)
                return NILVALUE;

            var result = RenderPropertyValue(propertyValue);

            // Conform to the RFC's restrictions
            result = result
                .AsPrintableAscii()
                .WithMaxLength(32);

            return result.Length >= 1
                ? result
                : NILVALUE;
        }

        private static string RenderStructuredData(LogEvent logEvent)
        {
            var properties = logEvent.Properties.Select(kvp =>
                new KeyValuePair<string, string>(RenderPropertyKey(kvp.Key), RenderPropertyValue(kvp.Value)));

            var structuredDataKvps = String.Join(" ", properties.Select(t => $@"{t.Key}=""{t.Value}"""));
            var structuredData = String.IsNullOrEmpty(structuredDataKvps) ? NILVALUE : $"[{STRUCTURED_DATA_ID} {structuredDataKvps}]";

            return structuredData;
        }

        private static string RenderPropertyKey(string propertyKey)
        {
            // Conform to the RFC's restrictions
            var result = propertyKey.AsPrintableAscii();

            // Also remove any '=', ']', and '"", as these are also not permitted in structured data parameter names
            // Unescaped regex pattern: [=\"\]]
            result = Regex.Replace(result, "[=\\\"\\]]", String.Empty);

            return result.WithMaxLength(32);
        }

        /// <summary>
        /// All Serilog property values are quoted, which is unnecessary, as we are going to encase them in
        /// quotes anyway, to conform to the specification for syslog structured data values - so this
        /// removes them and also unescapes any others
        /// </summary>
        private static string RenderPropertyValue(LogEventPropertyValue propertyValue)
        {
            // Trim surrounding quotes, and unescape all others
            var result = propertyValue
                .ToString()
                .TrimAndUnescapeQuotes();

            // Use a backslash to escape backslashes, double quotes and closing square brackets
            return Regex.Replace(result, @"[\]\\""]", match => $@"\{match}");
        }
    }
}
