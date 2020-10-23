// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Serilog.Sinks.Syslog
{
    /// <summary>
    /// Used to frame individual syslog messages so they can be sent over TCP and parsed correctly
    /// by the receiving syslog server
    /// </summary>
    public class MessageFramer
    {
        private static readonly byte CR = 0x0D;
        private static readonly byte LF = 0x0A;
        private static readonly byte NUL = 0x00;
        private static readonly byte[] CRLF = { 0x0D, 0x0A };

        private readonly FramingType framingType;
        private readonly Encoding encoding;

        public MessageFramer(FramingType framingType, Encoding encoding = null)
        {
            this.framingType = framingType;
            this.encoding = encoding ?? Encoding.UTF8;
        }

        public async Task WriteFrame(string message, Stream stream)
        {
            var data = this.encoding.GetBytes(message);

            if (this.framingType == FramingType.OCTET_COUNTING)
            {
                var len = Encoding.ASCII.GetBytes(data.Length.ToString());

                await stream.WriteAsync(len, 0, len.Length).ConfigureAwait(false);
                stream.WriteByte(32); // Space
            }

            await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);

            if (this.framingType != FramingType.OCTET_COUNTING)
            {
                switch (this.framingType)
                {
                    case FramingType.CRLF:
                        await stream.WriteAsync(CRLF, 0, 2).ConfigureAwait(false);
                        break;
                    case FramingType.CR:
                        stream.WriteByte(CR);
                        break;
                    case FramingType.LF:
                        stream.WriteByte(LF);
                        break;
                    case FramingType.NUL:
                        stream.WriteByte(NUL);
                        break;
                    default:
                        throw new ArgumentException($"Invalid message framing type: {this.framingType}");
                }
            }

            await stream.FlushAsync().ConfigureAwait(false);
        }
    }
}
