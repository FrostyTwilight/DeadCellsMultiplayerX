using Nerdbank.Streams;
using PolyType.SourceGenerator;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static dc.ui.OptionsSection;

namespace DeadCellsMultiplayerX.Utils
{
    internal static class StreamUtils
    {
        public static Stream CreateMultiplexingStream(this Stream stream, out MultiplexingStream result)
        {
            result = MultiplexingStream.Create(stream, new()
            {
                ProtocolMajorVersion = 3,
                SeededChannels =
                {
                    new()
                }
            });
            return result.AcceptChannel(0).AsStream();
        }

        public static JsonRpc CreateJsonRpc(this Stream stream)
        {
            var rpcChannel = stream.CreateMultiplexingStream(out var mxstream);
            var formatter = new NerdbankMessagePackFormatter
            {
                MultiplexingStream = mxstream,
                TypeShapeProvider = TypeShapeProvider_DeadCellsMultiplayerX.Default,
            };
            var handler = new LengthHeaderMessageHandler(rpcChannel, rpcChannel, formatter);
            var rpc = new JsonRpc(handler)
            {
                TraceSource = new TraceSource("JSON-RPC", SourceLevels.Warning)
            };
            rpc.TraceSource.Listeners.Add(new ConsoleTraceListener(true));
            return rpc;
        }


        public static async Task<string> ReadGuestGuidAsync(Stream stream)
        {
            const int GuidLength = 36;
            var buf = new byte[GuidLength];
            int read = 0;
            while (read < GuidLength)
            {
                int n = await stream.ReadAsync(buf.AsMemory(read, GuidLength - read))
                                    .ConfigureAwait(false);
                if (n == 0)
                    throw new EndOfStreamException(
                        $"Stream ended while reading guest GUID ({read}/{GuidLength} bytes).");
                read += n;
            }
            return Encoding.UTF8.GetString(buf);
        }
    }
}
