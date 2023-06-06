using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.Internal
{
    /// <summary>
    /// This Sink does nothing
    /// </summary>
    internal class NullSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            //Do nothing
        }
    }
}
