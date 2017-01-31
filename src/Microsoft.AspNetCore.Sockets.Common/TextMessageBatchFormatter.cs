using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;

namespace Microsoft.AspNetCore.Sockets
{
    public static class TextMessageBatchFormatter
    {
        public static IEnumerable<Message> ReadMessages(ReadOnlySpan<byte> data)
        {
            if (data.Length < 1 || data[0] != 'T')
            {
                throw new FormatException("Missing 'T' prefix in Text Message Batch.");
            }

            int cursor = 1;
            while (cursor < data.Length)
            {

                // Parse the length
                int length = ParseLength(data, ref cursor);
                cursor++;

                // Parse the type
                var type = ParseType(data, ref cursor);
                cursor++;

                // Read the payload
                var payload = ParsePayload(data, length, type, ref cursor);
                cursor++;

                yield return new Message(payload, type, endOfMessage: true);
            }
        }

        private static PreservedBuffer ParsePayload(ReadOnlySpan<byte> data, int length, MessageType messageType, ref int cursor)
        {
            int start = cursor;

            // We know exactly where the end is. The last byte is cursor + length
            cursor += length;

            // Verify the length and trailer
            if (cursor >= data.Length)
            {
                throw new FormatException("Unexpected end-of-message while reading Payload field.");
            }
            if (data[cursor] != ';')
            {
                throw new FormatException("Payload is missing trailer character ';'.");
            }

            // Read the data into a buffer
            var buffer = new byte[length];
            data.Slice(start, length).CopyTo(buffer);

            // If the message is binary, we need to convert from Base64
            if (messageType == MessageType.Binary)
            {
                // TODO: Use System.Binary.Base64 to handle this with less allocation

                // Parse the data as Base64
                var str = Encoding.UTF8.GetString(buffer);
                buffer = Convert.FromBase64String(str);
            }

            return ReadableBuffer.Create(buffer).Preserve();
        }

        private static MessageType ParseType(ReadOnlySpan<byte> data, ref int cursor)
        {
            int start = cursor;

            // Scan to a ':'
            cursor = IndexOf((byte)':', data, cursor);
            if (cursor >= data.Length)
            {
                throw new FormatException("Unexpected end-of-message while reading Type field.");
            }

            if (cursor - start != 1)
            {
                throw new FormatException("Type field must be exactly one byte long.");
            }

            switch (data[cursor - 1])
            {
                case (byte)'T': return MessageType.Text;
                case (byte)'B': return MessageType.Binary;
                case (byte)'C': return MessageType.Close;
                case (byte)'E': return MessageType.Error;
                default: throw new FormatException($"Unknown Type value: '{(char)data[cursor - 1]}'.");
            }
        }

        private static int ParseLength(ReadOnlySpan<byte> data, ref int cursor)
        {
            int start = cursor;

            // Scan to a ':'
            cursor = IndexOf((byte)':', data, cursor);
            if (cursor >= data.Length)
            {
                throw new FormatException("Unexpected end-of-message while reading Length field.");
            }

            // Parse the length
            int length = 0;
            for (int i = start; i < cursor; i++)
            {
                if (data[i] < '0' || data[i] > '9')
                {
                    throw new FormatException("Invalid length.");
                }
                length = (length * 10) + (data[i] - '0');
            }

            return length;
        }

        private static int IndexOf(byte c, ReadOnlySpan<byte> data, int start)
        {
            // Scan to the end or to the matching character
            int cursor = start;
            for (; cursor < data.Length && data[cursor] != c; cursor++) ;
            return cursor;
        }
    }
}
