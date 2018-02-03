using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.Syslog.Tests
{
    public static class StreamExtensions
    {
        private const byte SPACE = 0x20;
        private const byte END_OF_STREAM = 0xFF;

        /// <summary>
        /// Read the length of a syslog message that has been formatted using the octet-counting
        /// method described in RFC5425 and RFC6587
        /// </summary>
        /// <remarks>
        /// This is a rather 'odd' wire format, in that the message length is encoded as ASCII text,
        /// rather than being, for example, an integer encoded in a fixed 4 byte header
        /// </remarks>
        /// <param name="stream">The stream to read data from</param>
        public static int ReadLength(this Stream stream)
        {
            bool done = false;
            var buffer = new byte[10];
            int bytesRead = 0;

            while (!done)
            {
                var b = (byte)stream.ReadByte();

                // Client disconnected
                if (b == END_OF_STREAM)
                    throw new EndOfStreamException();

                if (b == SPACE)
                {
                    // We found a space character, so we're done reading the message length
                    done = true;
                }
                else
                {
                    buffer[bytesRead] = b;
                    bytesRead++;
                }
            }

            var len = Encoding.ASCII.GetString(buffer, 0, bytesRead);

            return Int32.Parse(len);
        }

        /// <summary>
        /// Read a specific number of bytes from a stream
        /// </summary>
        /// <param name="stream">The stream to read data from</param>
        /// <param name="count">The number of bytes to read</param>
        /// <param name="ct">Cancellation token</param>
        public static async Task<byte[]> ReadBytes(this Stream stream, int count, CancellationToken ct)
        {
            var buffer = new byte[count];
            int offset = 0;

            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer, offset, count - offset, ct);

                // Client disconnected
                if (read == 0)
                    throw new EndOfStreamException();

                offset += read;
            }

            return buffer;
        }
    }
}
