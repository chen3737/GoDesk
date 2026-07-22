using System.Buffers.Binary;

namespace GoDesk.Protocol.Control;

public static class ControlFrameCodec
{
    public static byte[] Encode(ControlMessageType messageType, ReadOnlySpan<byte> payload)
    {
        if (!Enum.IsDefined(messageType))
        {
            throw new ProtocolFrameException("The control message type is unknown.");
        }

        if (payload.Length > ProtocolLimits.MaxControlPayloadBytes)
        {
            throw new ProtocolFrameException("The control payload exceeds 64 KiB.");
        }

        byte[] frame = new byte[ProtocolLimits.ControlHeaderBytes + payload.Length];
        Span<byte> header = frame.AsSpan(0, ProtocolLimits.ControlHeaderBytes);
        BinaryPrimitives.WriteUInt16BigEndian(header, ProtocolLimits.CurrentVersion);
        BinaryPrimitives.WriteUInt16BigEndian(header[2..], (ushort)messageType);
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], (uint)payload.Length);
        payload.CopyTo(frame.AsSpan(ProtocolLimits.ControlHeaderBytes));
        return frame;
    }

    public static ControlFrame Decode(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < ProtocolLimits.ControlHeaderBytes)
        {
            throw new ProtocolFrameException("The control frame header is truncated.");
        }

        ushort version = BinaryPrimitives.ReadUInt16BigEndian(frame);
        ushort rawType = BinaryPrimitives.ReadUInt16BigEndian(frame[2..]);
        uint rawLength = BinaryPrimitives.ReadUInt32BigEndian(frame[4..]);

        if (version != ProtocolLimits.CurrentVersion)
        {
            throw new ProtocolFrameException("The protocol version is unsupported.");
        }

        var messageType = (ControlMessageType)rawType;
        if (!Enum.IsDefined(messageType))
        {
            throw new ProtocolFrameException("The control message type is unknown.");
        }

        if (rawLength > ProtocolLimits.MaxControlPayloadBytes)
        {
            throw new ProtocolFrameException("The advertised payload exceeds 64 KiB.");
        }

        int expectedLength = checked(ProtocolLimits.ControlHeaderBytes + (int)rawLength);
        if (frame.Length != expectedLength)
        {
            throw new ProtocolFrameException("The control frame length does not match its header.");
        }

        byte[] payload = frame[ProtocolLimits.ControlHeaderBytes..].ToArray();
        return ControlFrame.FromOwnedPayload(
            version,
            messageType,
            payload);
    }
}
