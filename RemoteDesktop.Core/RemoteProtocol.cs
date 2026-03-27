using System.Buffers.Binary;
using System.Net.Sockets;

namespace RemoteDesktop.Core;

public enum PacketType : byte
{
    Frame = 1,
    MouseMove = 2,
    MouseDown = 3,
    MouseUp = 4,
    KeyDown = 5,
    KeyUp = 6,
    MouseWheel = 7,
    TextInput = 8
}

public enum RemoteMouseButton : byte
{
    Left = 1,
    Right = 2,
    Middle = 3
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

    public static byte[] BuildMouseMovePayload(int x, int y)
    {
        byte[] payload = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(0, 4), x);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(4, 4), y);
        return payload;
    }

    public static (int X, int Y) ParseMouseMovePayload(byte[] payload)
    {
        if (payload.Length != 8)
        {
            throw new InvalidOperationException($"MouseMove payload must be 8 bytes, got {payload.Length}.");
        }

        int x = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(0, 4));
        int y = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(4, 4));
        return (x, y);
    }

    public static byte[] BuildMouseButtonPayload(RemoteMouseButton button)
    {
        return [(byte)button];
    }

    public static RemoteMouseButton ParseMouseButtonPayload(byte[] payload)
    {
        if (payload.Length != 1)
        {
            throw new InvalidOperationException($"Mouse button payload must be 1 byte, got {payload.Length}.");
        }

        return (RemoteMouseButton)payload[0];
    }

    public static byte[] BuildKeyPayload(int virtualKey)
    {
        byte[] payload = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(0, 4), virtualKey);
        return payload;
    }

    public static int ParseKeyPayload(byte[] payload)
    {
        if (payload.Length != 4)
        {
            throw new InvalidOperationException($"Key payload must be 4 bytes, got {payload.Length}.");
        }

        return BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(0, 4));
    }

    public static byte[] BuildMouseWheelPayload(int delta)
    {
        byte[] payload = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(0, 4), delta);
        return payload;
    }

    public static int ParseMouseWheelPayload(byte[] payload)
    {
        if (payload.Length != 4)
        {
            throw new InvalidOperationException($"Mouse wheel payload must be 4 bytes, got {payload.Length}.");
        }

        return BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(0, 4));
    }

    public static byte[] BuildTextInputPayload(string text)
    {
        return System.Text.Encoding.UTF8.GetBytes(text);
    }

    public static string ParseTextInputPayload(byte[] payload)
    {
        return System.Text.Encoding.UTF8.GetString(payload);
    }
}
