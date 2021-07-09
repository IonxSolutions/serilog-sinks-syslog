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
        private static readonly Random rng = new Random();

        private static async Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(config)
                .CreateLogger();

            SelfLog.Enable(Console.Error);

            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.WriteLine("Received stop signal");
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            await WriteLogs(cts.Token);

            // Blocks here waiting for CTRL-C
            Console.WriteLine("Press CTRL-C to stop");
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
