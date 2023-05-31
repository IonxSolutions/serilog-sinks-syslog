// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog.Debugging;

namespace Serilog.Sinks.Syslog.Sample
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            // Examples for both reading configuration from a JSON configuration file and
            // an App.config/web.config file. The latter requiring the Serilog.Settings.AppSettings
            // NuGet package. See https://github.com/serilog/serilog/wiki/AppSettings
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(config)
                .ReadFrom.AppSettings()
                .CreateLogger();

            SelfLog.Enable(Console.Error);

            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.WriteLine("Received stop signal");
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            Console.WriteLine("Press CTRL-C to stop");
            await WriteLogs(cts.Token);

            // Blocks here waiting for CTRL-C
            cts.Token.WaitHandle.WaitOne();

            Log.CloseAndFlush();
        }

        private static async Task WriteLogs(CancellationToken ct)
        {
            int i = 1;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    Log.Information("This is test message {MessageNumber:00000}", i);
                    await Task.Delay(2000, ct);

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
