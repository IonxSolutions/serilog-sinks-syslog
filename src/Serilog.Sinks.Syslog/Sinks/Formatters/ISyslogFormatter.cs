// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using Serilog.Events;

namespace Serilog.Sinks.Syslog
{
    public interface ISyslogFormatter
    {
        string FormatMessage(LogEvent logEvent);
        int CalculatePriority(LogEventLevel level);
    }
}
