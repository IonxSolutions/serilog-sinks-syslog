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
using Serilog.Sinks.Syslog;

namespace Serilog
{
    /// <summary>
    /// Extends Serilog configuration to write events to a remote syslog service, or to the local syslog
    /// service on Linux systems
    /// </summary>
    public static class SyslogLoggerConfigurationExtensions
    {
        /// <summary>
        /// Adds a sink that writes log events to the local syslog service on a Linux system
        /// </summary>
        /// <param name="loggerSinkConfig">The logger configuration</param>
        /// <param name="appName">The name of the application. Defaults to the current process name</param>
        /// <param name="facility">The category of the application</param>
        /// <param name="outputTemplate">A message template describing the output messages
        /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink</param>
        /// <seealso cref="https://github.com/serilog/serilog/wiki/Formatting-Output"/>
        /// </param>
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
        /// <param name="appName">The name of the application. Defaults to the current process name</param>
        /// <param name="format">The syslog message format to be used</param>
        /// <param name="facility">The category of the application</param>
        /// <param name="outputTemplate">A message template describing the output messages
        /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink</param>
        /// <seealso cref="https://github.com/serilog/serilog/wiki/Formatting-Output"/>
        /// </param>
        public static LoggerConfiguration UdpSyslog(this LoggerSinkConfiguration loggerSinkConfig,
            string host, int port = 514, string appName = null, SyslogFormat format = SyslogFormat.RFC3164,
            Facility facility = Facility.Local0, string outputTemplate = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum)
        {
            if (String.IsNullOrWhiteSpace(host))
                throw new ArgumentException(nameof(host));

            var formatter = GetFormatter(format, appName, facility, outputTemplate);
            var endpoint = ResolveIP(host, port);

            var sink = new SyslogUdpSink(endpoint, formatter, BatchConfig.Default);

            return loggerSinkConfig.Sink(sink, restrictedToMinimumLevel);
        }

        /// <summary>
        /// Adds a sink that writes log events to a TCP syslog server, optionally over a TLS-secured
        /// channel
        /// </summary>
        /// <param name="loggerSinkConfig">The logger configuration</param>
        /// <param name="config">Defines how to interact with the syslog server</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink</param>
        public static LoggerConfiguration TcpSyslog(this LoggerSinkConfiguration loggerSinkConfig,
            SyslogTcpConfig config, LogEventLevel restricedToMinimumLevel = LevelAlias.Minimum)
        {
            if (String.IsNullOrWhiteSpace(config.Host))
                throw new ArgumentException(nameof(config.Host));

            var sink = new SyslogTcpSink(config, BatchConfig.Default);

            return loggerSinkConfig.Sink(sink, restricedToMinimumLevel);
        }

        /// <summary>
        /// Adds a sink that writes log events to a TCP syslog server, optionally over a TLS-secured
        /// </summary>
        /// <param name="loggerSinkConfig">The logger configuration</param>
        /// <param name="host">Hostname of the syslog server</param>
        /// <param name="port">Port the syslog server is listening on</param>
        /// <param name="appName">The name of the application. Defaults to the current process name</param>
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
        /// <param name="outputTemplate">A message template describing the output messages
        /// <seealso cref="https://github.com/serilog/serilog/wiki/Formatting-Output"/>
        /// </param>
        /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink</param>
        public static LoggerConfiguration TcpSyslog(this LoggerSinkConfiguration loggerSinkConfig,
            string host, int port = 1468, string appName = null, FramingType framingType = FramingType.OCTET_COUNTING,
            SyslogFormat format = SyslogFormat.RFC5424, Facility facility = Facility.Local0,
            SslProtocols secureProtocols = SslProtocols.Tls12, ICertificateProvider certProvider = null,
            RemoteCertificateValidationCallback certValidationCallback = null,
            string outputTemplate = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum)
        {
            var formatter = GetFormatter(format, appName, facility, outputTemplate);

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

            return TcpSyslog(loggerSinkConfig, config, restrictedToMinimumLevel);
        }

        private static ISyslogFormatter GetFormatter(SyslogFormat format, string appName, Facility facility,
            string outputTemplate)
        {
            var templateFormatter = String.IsNullOrWhiteSpace(outputTemplate)
                ? null
                : new MessageTemplateTextFormatter(outputTemplate, null);

            switch (format)
            {
                case SyslogFormat.RFC3164:
                    return new Rfc3164Formatter(facility, appName, templateFormatter);
                case SyslogFormat.RFC5424:
                    return new Rfc5424Formatter(facility, appName, templateFormatter);
                case SyslogFormat.Local:
                    return new LocalFormatter(facility, templateFormatter);
                default:
                    throw new ArgumentException($"Invalid format: {format}");
            }
        }

        private static IPEndPoint ResolveIP(string host, int port)
        {
            if (!IPAddress.TryParse(host, out var addr))
            {
                addr = Dns.GetHostAddresses(host)
                    .First(x => x.AddressFamily == AddressFamily.InterNetwork
                    || x.AddressFamily == AddressFamily.InterNetworkV6);
            }

            return new IPEndPoint(addr, port);
        }
    }
}
