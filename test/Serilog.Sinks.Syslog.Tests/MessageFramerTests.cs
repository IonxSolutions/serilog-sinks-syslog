// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Shouldly;

namespace Serilog.Sinks.Syslog.Tests
{
    public class MessageFramerTests
    {
        private readonly string message = "This is a test message";
        private const byte SPACE = 0x20;

        [Theory]
        [InlineData(FramingType.CR, new byte[] { 0x0D })]
        [InlineData(FramingType.LF, new byte[] { 0x0A })]
        [InlineData(FramingType.NUL, new byte[] { 0x00 })]
        [InlineData(FramingType.CRLF, new byte[] { 0x0D, 0x0A })]
        public async Task Should_use_non_transparent_framing(FramingType framingType, byte[] expectedSuffix)
        {
            var framer = new MessageFramer(framingType);

            using (var ms = new MemoryStream())
            {
                await framer.WriteFrame(this.message, ms);

                var data = ms.ToArray();
                data.EndsWith(expectedSuffix).ShouldBeTrue();
            }
        }

        [Fact]
        public async Task Should_use_transparent_framing()
        {
            var framer = new MessageFramer(FramingType.OCTET_COUNTING);

            using (var ms = new MemoryStream())
            {
                await framer.WriteFrame(this.message, ms);

                var data = ms.ToArray();
                var prefix = data.TakeWhile(b => b != SPACE).ToArray();
                var msgLen = Encoding.UTF8.GetString(prefix).ToInt();

                // The length of the whole frame should be:
                // - The length of the prefix containing the syslog message length
                // - The separating space character
                // - The length of the syslog message, as specified by the prefix
                data.Length.ShouldBe(prefix.Length + 1 + msgLen);

                var framedMessage = Encoding.UTF8.GetString(data);
                framedMessage.ShouldBe($"{msgLen} {this.message}");
            }
        }
    }

    public static class ByteArrayExtensions
    {
        public static bool EndsWith(this byte[] subject, byte[] suffix)
        {
            if (subject.Length < suffix.Length)
                return false;

            var lastIdx = subject.Length - 1;

            for (var i = suffix.Length - 1; i >= 0; i--)
            {
                if (suffix[i] != subject[lastIdx])
                    return false;

                lastIdx--;
            }

            return true;
        }
    }
}
