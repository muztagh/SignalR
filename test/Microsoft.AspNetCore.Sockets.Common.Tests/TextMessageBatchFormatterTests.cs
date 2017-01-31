using System;
using System.Linq;
using System.Text;
using Xunit;

namespace Microsoft.AspNetCore.Sockets.Tests
{
    public class TextMessageBatchFormatterTests
    {
        [Theory]
        [InlineData("T0:T:;", MessageType.Text, "")]
        [InlineData("T3:T:ABC;", MessageType.Text, "ABC")]
        [InlineData("T11:T:A\nR\rC\r\n;DEF;", MessageType.Text, "A\nR\rC\r\n;DEF")]
        [InlineData("T0:C:;", MessageType.Close, "")]
        [InlineData("T17:C:Connection Closed;", MessageType.Close, "Connection Closed")]
        [InlineData("T0:E:;", MessageType.Error, "")]
        [InlineData("T12:E:Server Error;", MessageType.Error, "Server Error")]
        public void ReadSingleTextMessage(string data, MessageType messageType, string payload)
        {
            var buffer = Encoding.UTF8.GetBytes(data);
            var messages = TextMessageBatchFormatter.ReadMessages(buffer).ToArray();

            Assert.Equal(1, messages.Length);
            AssertMessage(messages[0], messageType, payload);
        }

        [Theory]
        [InlineData("T0:B:;", new byte[0])]
        [InlineData("T8:B:q83vEg==;", new byte[] { 0xAB, 0xCD, 0xEF, 0x12 })]
        public void ReadSingleBinaryMessage(string data, byte[] payload)
        {
            var buffer = Encoding.UTF8.GetBytes(data);
            var messages = TextMessageBatchFormatter.ReadMessages(buffer).ToArray();

            Assert.Equal(1, messages.Length);
            AssertMessage(messages[0], MessageType.Binary, payload);
        }

        [Fact]
        public void ReadMultipleMessages()
        {
            const string data = "T0:B:;14:T:Hello,\r\nWorld!;1:C:A;12:E:Server Error;";
            var buffer = Encoding.UTF8.GetBytes(data);
            var messages = TextMessageBatchFormatter.ReadMessages(buffer).ToArray();

            Assert.Equal(4, messages.Length);
            AssertMessage(messages[0], MessageType.Binary, new byte[0]);
            AssertMessage(messages[1], MessageType.Text, "Hello,\r\nWorld!");
            AssertMessage(messages[2], MessageType.Close, "A");
            AssertMessage(messages[3], MessageType.Error, "Server Error");
        }

        [Theory]
        [InlineData("", "Missing 'T' prefix in Text Message Batch.")]
        [InlineData("ABC", "Missing 'T' prefix in Text Message Batch.")]
        [InlineData("T1230450945", "Unexpected end-of-message while reading Length field.")]
        [InlineData("T12ab34:", "Invalid length.")]
        [InlineData("T1:asdf", "Unexpected end-of-message while reading Type field.")]
        [InlineData("T1::", "Type field must be exactly one byte long.")]
        [InlineData("T1:AB:", "Type field must be exactly one byte long.")]
        [InlineData("T5:T:A", "Unexpected end-of-message while reading Payload field.")]
        [InlineData("T5:T:ABCDE", "Unexpected end-of-message while reading Payload field.")]
        [InlineData("T5:T:ABCDEF", "Payload is missing trailer character ';'.")]
        public void InvalidMessages(string data, string message)
        {
            var ex = Assert.Throws<FormatException>(() => TextMessageBatchFormatter.ReadMessages(Encoding.UTF8.GetBytes(data)).ToArray());
            Assert.Equal(message, ex.Message);
        }

        private static void AssertMessage(Message message, MessageType messageType, byte[] payload)
        {
            Assert.True(message.EndOfMessage);
            Assert.Equal(messageType, message.Type);
            Assert.Equal(payload, message.Payload.Buffer.ToArray());
        }

        private static void AssertMessage(Message message, MessageType messageType, string payload)
        {
            Assert.True(message.EndOfMessage);
            Assert.Equal(messageType, message.Type);
            Assert.Equal(payload, Encoding.UTF8.GetString(message.Payload.Buffer.ToArray()));
        }
    }
}
