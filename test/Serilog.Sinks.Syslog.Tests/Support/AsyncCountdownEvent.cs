using System;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.Syslog.Tests
{
    public class AsyncCountdownEvent
    {
        private readonly TaskCompletionSource<bool> tcs;
        private int count;

        public AsyncCountdownEvent(int count)
        {
            this.tcs = new TaskCompletionSource<bool>();
            this.count = count;
        }

        public Task WaitAsync(TimeSpan timeout, CancellationToken ct)
        {
            return Task.WhenAny(this.tcs.Task, CreateDelayTask(timeout, ct));
        }

        public Task WaitAsync(int timeout, CancellationToken ct)
        {
            return WaitAsync(TimeSpan.FromSeconds(timeout), ct);
        }

        public void Signal()
        {
            if (Interlocked.Decrement(ref this.count) == 0)
            {
                this.tcs.SetResult(true);
            }
        }

        private static async Task CreateDelayTask(TimeSpan timeout, CancellationToken ct)
        {
            try
            {
                await Task.Delay(timeout, ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Caller is responsible for detecting which task completed
            }
        }
    }
}
