// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System.Runtime.InteropServices;
using Xunit;

namespace Serilog.Sinks.Syslog.Tests
{
    public class LinuxOnlyFact : FactAttribute
    {
        public LinuxOnlyFact()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Skip = "This test should only be run on Linux systems";
            }
        }
    }
}
