using System.Buffers.Binary;
using System.Net.Sockets;

namespace RemoteDesktop.Core;

public enum PacketType : byte
{
    Frame = 1
}

public static class RemoteProtocol
{
    public static async Task WritePacketAsync(NetworkStream stream, PacketType packetType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        byte[] header = new byte[5];
        header[0] = (byte)packetType;
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(1, 4), payload.Length);

        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
    }

    public static async Task<(PacketType PacketType, byte[] Payload)> ReadPacketAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        byte[] header = new byte[5];
        await ReadExactAsync(stream, header, cancellationToken);

        PacketType packetType = (PacketType)header[0];
        int payloadLength = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(1, 4));
        if (payloadLength < 0 || payloadLength > 20 * 1024 * 1024)
        {
            throw new InvalidOperationException($"Invalid payload length: {payloadLength}");
        }

        byte[] payload = new byte[payloadLength];
        await ReadExactAsync(stream, payload, cancellationToken);
        return (packetType, payload);
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (read == 0)
            {
                throw new IOException("Connection closed by remote endpoint.");
            }

            totalRead += read;
        }
    }
}
