// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Context;
using Serilog.Debugging;
using Serilog.Formatting.Display;

namespace Serilog.Sinks.Syslog.Sample
{
    internal static class Program
    {
        private static readonly Random rng = new Random();
        private static readonly string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private static async Task Main(string[] args)
        {
            var logConfig = new LoggerConfiguration()
                .WriteTo.Console();

            // Use custom message template formatters, just so we can distinguish which sink wrote each message
            var tcpTemplateFormatter = new MessageTemplateTextFormatter("TCP: {Message}", null);
            const string udpOutputTemplate = "UDP: {Message}";
            const string localOutputTemplate = "Local: {Message}";

            // The LocalSyslog sink is only supported on Linux
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                logConfig.WriteTo.LocalSyslog(outputTemplate: localOutputTemplate);
            }

            var certFilename = Path.Combine(baseDir, "client.p12");

            // Log RFC5424 formatted events to a syslog server listening on TCP 6514, using a TLS-secured
            // channel, with mutual authentication
            var tcpConfig = new SyslogTcpConfig
            {
                Host = "192.168.0.175",
                Port = 6514,
                Formatter = new Rfc5424Formatter(templateFormatter: tcpTemplateFormatter),
                Framer = new MessageFramer(FramingType.OCTET_COUNTING),
                SecureProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
                CertProvider = new CertificateFileProvider(certFilename, String.Empty),
                CertValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    // Allow all certs (don't do this in production!)
                    return true;
                }
            };

            var log = logConfig
                .WriteTo.UdpSyslog("192.168.0.175", 514, outputTemplate: udpOutputTemplate)
                .WriteTo.TcpSyslog(tcpConfig)
                .Enrich.FromLogContext()
                .CreateLogger();

            SelfLog.Enable(Console.Error);

            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.WriteLine("Received stop signal");
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            await WriteLogs(log, cts.Token);

            // Blocks here waiting for CTRL-C
            Console.WriteLine("Press CTRL-C to stop");
            cts.Token.WaitHandle.WaitOne();
        }

        private static async Task WriteLogs(ILogger log, CancellationToken ct)
        {
            int i = 1;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (i % 2 == 0)
                    {
                        using (LogContext.PushProperty("AProperty", rng.NextDouble()))
                        {
                            log.Information("This is test message {MessageNumber:00000}", i);
                            await Task.Delay(2000, ct);
                        }
                    }
                    else
                    {
                        log.Information("This is test message {MessageNumber:00000}", i);
                        await Task.Delay(2000, ct);
                    }

                    i++;
                }
            }
            catch (TaskCanceledException)
            {
                // Exiting
            }
        }
    }
}
