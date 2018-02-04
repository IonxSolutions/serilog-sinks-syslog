// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System.Runtime.InteropServices;
using Xunit;

namespace Serilog.Sinks.Syslog.Tests
{
    public class WindowsOnlyFact : FactAttribute
    {
        public WindowsOnlyFact()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Skip = "This test should only be run on Windows systems";
            }
        }
    }
}
