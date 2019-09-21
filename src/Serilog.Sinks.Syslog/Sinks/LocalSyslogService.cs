// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Runtime.InteropServices;

namespace Serilog.Sinks.Syslog
{
    public class LocalSyslogService
    {
        /// <summary>
        /// Opens a connection to the local syslog service
        /// </summary>
        /// <param name="ident">
        /// Prepended to every message, and typically set to the name of the calling program
        /// </param>
        /// <param name="option">
        /// Flags that control how the connection to syslog is opened, and how messages are
        /// subsequently logged
        /// </param>
        /// <param name="facility">
        /// The default facility to be used if none is specified in subsequent calls to syslog()
        /// </param>
        [DllImport("libc")]
        private static extern void openlog(IntPtr ident, SyslogOptions option, Facility facility);

        /// <summary>
        /// Write a message to the local syslog service.
        /// The libc syslog method takes a printf-style format string and a variable number of
        /// arguments required by the format - we can't do this from C#, so the caller must use
        /// format string "%s" with a single message argument
        /// </summary>
        /// <param name="priority">Calculated using: Facility * 8 + Severity</param>
        /// <param name="format">Format string: should always be "%s"</param>
        /// <param name="message">The RFC3164 or RFC5424 formatted syslog message to be logged</param>
        [DllImport("libc", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void syslog(int priority, string format, string message);

        /// <summary>
        /// Close the connection to the local syslog service
        /// </summary>
        [DllImport("libc")]
        private static extern void closelog();

        private readonly Facility facility;
        private readonly string appIdentity;
        private IntPtr appIdentityHandle = IntPtr.Zero;

        public LocalSyslogService(Facility facility, string appIdentity = null)
        {
            this.facility = facility;
            this.appIdentity = appIdentity ?? AppDomain.CurrentDomain.FriendlyName;
        }

        /// <summary>
        /// Opens a connection to the local syslog service
        /// </summary>
        public virtual void Open()
        {
            this.appIdentityHandle = Marshal.StringToHGlobalAnsi(this.appIdentity ?? AppDomain.CurrentDomain.FriendlyName);

            openlog(this.appIdentityHandle, SyslogOptions.LOG_PID, this.facility);
        }

        /// <summary>
        /// Write a message to the local syslog service
        /// </summary>
        /// <param name="priority">
        /// Priority of the message to log. Calculated using: Facility * 8 + Severity
        /// </param>
        /// <param name="message">The RFC3164 or RFC5424 formatted syslog message to be logged</param>
        public virtual void WriteLog(int priority, string message)
        {
            syslog(priority, "%s", message);
        }

        /// <summary>
        /// Close the connection to the local syslog service
        /// </summary>
        public virtual void Close()
        {
            closelog();

            if (this.appIdentityHandle != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(this.appIdentityHandle);
            }
        }
    }
}
