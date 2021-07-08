// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.PeriodicBatching;
using Serilog.Sinks.Syslog;

// Allow the unit tests to access the internal DefaultBatchOptions variable so as to be able
// to derive a timeout value based upon the time interval that the batched messages are sent.
// Granted, the default PeriodicBatchingSinkOptions also has the EagerlyEmitFirstEvent set to
// true, so the Period is unlikely to come into effect. Nonetheless, it helps give the timeout
// a reason.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Serilog.Sinks.Syslog.Tests")]

namespace Serilog
{
    /// <summary>
    /// Extends Serilog configuration to write events to a remote syslog service, or to the local syslog
    /// service on Linux systems
    /// </summary>
    public static class SyslogLoggerConfigurationExtensions
    {
        internal static readonly PeriodicBatchingSinkOptions DefaultBatchOptions = new PeriodicBatchingSinkOptions
        {
            BatchSizeLimit = 1000,
            Period = TimeSpan.FromSeconds(2),
            QueueLimit = 100_000
        };

        /// <summary>
        /// Adds a sink that writes log events to the local syslog service on a Linux system
        /// </summary>
        /// <param name="loggerSinkConfig">The logger configuration</param>
        /// <param name="appName">The name of the application. Defaults to the current process name</param>
        /// <param name="facility">The category of the application</param>
        /// <param name="outputTemplate">A message template describing the output messages</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink</param>
        /// <seealso cref="!:https://github.com/serilog/serilog/wiki/Formatting-Output"/>
        public static LoggerConfiguration LocalSyslog(this LoggerSinkConfiguration loggerSinkConfig,
            string appName = null, Facility facility = Facility.Local0, string outputTemplate = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                throw new ArgumentException("The local syslog sink is only supported on Linux systems");

            var formatter = GetFormatter(SyslogFormat.Local, appName, facility, outputTemplate);
            var syslogService = new LocalSyslogService(facility, appName);
            syslogService.Open();

            var sink = new SyslogLocalSink(formatter, syslogService);

            return loggerSinkConfig.Sink(sink, restrictedToMinimumLevel);
        }

        /// <summary>
        /// Adds a sink that writes log events to a UDP syslog server
        /// </summary>
        /// <param name="loggerSinkConfig">The logger configuration</param>
        /// <param name="host">Hostname of the syslog server</param>
        /// <param name="port">Port the syslog server is listening on</param>
        /// <param name="appName">The name of the application. Must be all printable ASCII characters. Max length 32 (for RFC3164) or 48 (for RFC5424). Defaults to the current process name</param>
        /// <param name="format">The syslog message format to be used</param>
        /// <param name="facility">The category of the application</param>
        /// <param name="batchConfig">Batching configuration</param>
        /// <param name="outputTemplate">A message template describing the output messages</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink</param>
        /// <param name="messageIdPropertyName">Where the Id number of the message will be derived from. Only applicable when <paramref name="format"/> is <see cref="SyslogFormat.RFC5424"/>. Defaults to the "SourceContext" property of the syslog event. Property name and value must be all printable ASCII characters with max length of 32.</param>
        /// <param name="hostName">Name of the host. Must be in printable ASCII characters. Max length 255. Defaults to the machine name.</param>
        /// <see cref="!:https://github.com/serilog/serilog/wiki/Formatting-Output"/>
        public static LoggerConfiguration UdpSyslog(this LoggerSinkConfiguration loggerSinkConfig,
            string host, int port = 514, string appName = null, SyslogFormat format = SyslogFormat.RFC3164,
            Facility facility = Facility.Local0, PeriodicBatchingSinkOptions batchConfig = null, string outputTemplate = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string messageIdPropertyName = Rfc5424Formatter.DefaultMessageIdPropertyName,
            string hostName = "")
        {
            if (String.IsNullOrWhiteSpace(host))
                throw new ArgumentException(nameof(host));

            batchConfig ??= DefaultBatchOptions;
            var formatter = GetFormatter(format, appName, facility, outputTemplate, messageIdPropertyName, hostName);
            var endpoint = ResolveIP(host, port);

            var syslogUdpSink = new SyslogUdpSink(endpoint, formatter);
            var sink = new PeriodicBatchingSink(syslogUdpSink, batchConfig);

            return loggerSinkConfig.Sink(sink, restrictedToMinimumLevel);
        }

        /// <summary>
        /// Adds a sink that writes log events to a TCP syslog server, optionally over a TLS-secured channel
        /// </summary>
        /// <param name="loggerSinkConfig">The logger configuration</param>
        /// <param name="config">Defines how to interact with the syslog server</param>
        /// <param name="batchConfig">Batching configuration</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink</param>
        public static LoggerConfiguration TcpSyslog(this LoggerSinkConfiguration loggerSinkConfig,
            SyslogTcpConfig config, PeriodicBatchingSinkOptions batchConfig = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum)
        {
            if (String.IsNullOrWhiteSpace(config.Host))
                throw new ArgumentException(nameof(config.Host));

            batchConfig ??= DefaultBatchOptions;

            var syslogTcpSink = new SyslogTcpSink(config);
            var sink = new PeriodicBatchingSink(syslogTcpSink, batchConfig);

            return loggerSinkConfig.Sink(sink, restrictedToMinimumLevel);
        }

        /// <summary>
        /// Adds a sink that writes log events to a TCP syslog server, optionally over a TLS-secured
        /// </summary>
        /// <param name="loggerSinkConfig">The logger configuration</param>
        /// <param name="host">Hostname of the syslog server</param>
        /// <param name="port">Port the syslog server is listening on</param>
        /// <param name="appName">The name of the application. Must be all printable ASCII characters. Max length 32 (for RFC3164) or 48 (for RFC5424). Defaults to the current process name</param>
        /// <param name="framingType">How to frame/delimit syslog messages for the wire</param>
        /// <param name="format">The syslog message format to be used</param>
        /// <param name="facility">The category of the application</param>
        /// <param name="secureProtocols">
        /// SSL/TLS protocols to be used for a secure channel. Set to None for an unsecured connection
        /// </param>
        /// <param name="certProvider">Optionally used to present the syslog server with a client certificate</param>
        /// <param name="certValidationCallback">
        /// Optional callback used to validate the syslog server's certificate. If null, the system default
        /// will be used
        /// </param>
        /// <param name="outputTemplate">A message template describing the output messages</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink</param>
        /// <param name="messageIdPropertyName">Where the Id number of the message will be derived from. Only applicable when <paramref name="format"/> is <see cref="SyslogFormat.RFC5424"/>. Defaults to the "SourceContext" property of the syslog event. Property name and value must be all printable ASCII characters with max length of 32.</param>
        /// <param name="batchConfig">Configuration for the Periodic Batching Sink, type of PeriodicBatchingSinkOptions. Has the fields batchSizeLimit (Integer, defaults to 1000), batchPeriod (TimeSpan, defaults to 2 seconds) and batchQueueLimit (Nullable[int], defaults to 100.000</param>
        /// <param name="hostName">Name of the host. Must be in printable ASCII characters. Max length 255. Defaults to the machine name.</param>
        /// <seealso cref="!:https://github.com/serilog/serilog/wiki/Formatting-Output"/>
        public static LoggerConfiguration TcpSyslog(this LoggerSinkConfiguration loggerSinkConfig,
            string host, int port = 1468, string appName = null, FramingType framingType = FramingType.OCTET_COUNTING,
            SyslogFormat format = SyslogFormat.RFC5424, Facility facility = Facility.Local0,
            SslProtocols secureProtocols = SslProtocols.Tls12, ICertificateProvider certProvider = null,
            RemoteCertificateValidationCallback certValidationCallback = null,
            string outputTemplate = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string messageIdPropertyName = Rfc5424Formatter.DefaultMessageIdPropertyName,
            PeriodicBatchingSinkOptions batchConfig = null,
            string hostName = "")
        {
            var formatter = GetFormatter(format, appName, facility, outputTemplate, messageIdPropertyName, hostName);

            var config = new SyslogTcpConfig
            {
                Host = host,
                Port = port,
                Formatter = formatter,
                Framer = new MessageFramer(framingType),
                SecureProtocols = secureProtocols,
                CertProvider = certProvider,
                CertValidationCallback = certValidationCallback
            };

            batchConfig ??= DefaultBatchOptions;

            return TcpSyslog(loggerSinkConfig, config, batchConfig, restrictedToMinimumLevel);
        }

        private static ISyslogFormatter GetFormatter(SyslogFormat format, string appName, Facility facility,
            string outputTemplate,
            string messageIdPropertyName = null,
            string hostName = "")
        {
            var templateFormatter = String.IsNullOrWhiteSpace(outputTemplate)
                ? null
                : new MessageTemplateTextFormatter(outputTemplate, null);

            return format switch
            {
                SyslogFormat.RFC3164 => new Rfc3164Formatter(facility, appName, templateFormatter, hostName),
                SyslogFormat.RFC5424 => new Rfc5424Formatter(facility, appName, templateFormatter, messageIdPropertyName, hostName),
                SyslogFormat.Local => new LocalFormatter(facility, templateFormatter),
                _ => throw new ArgumentException($"Invalid format: {format}")
            };
        }

        private static IPEndPoint ResolveIP(string host, int port)
        {
            var addr = Dns.GetHostAddresses(host)
                .First(x => x.AddressFamily == AddressFamily.InterNetwork
                || x.AddressFamily == AddressFamily.InterNetworkV6);

            return new IPEndPoint(addr, port);
        }
    }
}
