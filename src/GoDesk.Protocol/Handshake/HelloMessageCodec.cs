using System.Formats.Cbor;
using GoDesk.Core.Identity;
using GoDesk.Protocol.Control;

namespace GoDesk.Protocol.Handshake;

public static class HelloMessageCodec
{
    public static byte[] Encode(HelloMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var writer = new CborWriter(CborConformanceMode.Canonical);
        writer.WriteStartMap(5);
        writer.WriteInt32(0);
        writer.WriteUInt64(message.ProtocolVersion);
        writer.WriteInt32(1);
        writer.WriteTextString(message.DeviceId.Value);
        writer.WriteInt32(2);
        writer.WriteByteString(message.Nonce.Span);
        writer.WriteInt32(3);
        writer.WriteUInt64(message.FeatureBits);
        writer.WriteInt32(4);
        writer.WriteInt64(message.UnixTimeMilliseconds);
        writer.WriteEndMap();
        return writer.Encode();
    }

    public static HelloMessage Decode(ReadOnlyMemory<byte> payload)
    {
        if (payload.Length > ProtocolLimits.MaxControlPayloadBytes)
        {
            throw new ProtocolFrameException("The hello payload exceeds 64 KiB.");
        }

        // Canonical conformance rejects a duplicated map key before ReadInt32 returns it.
        // Classify that case with a bounded shallow scan before the canonical parse.
        RejectDuplicatedTopLevelField(payload);

        try
        {
            var reader = new CborReader(payload, CborConformanceMode.Canonical);
            int? count = reader.ReadStartMap();
            if (count != 5)
            {
                throw new ProtocolFrameException("A hello message must contain five fields.");
            }

            ushort? version = null;
            DeviceId? deviceId = null;
            byte[]? nonce = null;
            ulong? featureBits = null;
            long? unixTimeMilliseconds = null;
            var seenKeys = new HashSet<int>();

            for (int index = 0; index < count.Value; index++)
            {
                int key = ReadFieldKey(reader);
                if (!seenKeys.Add(key))
                {
                    throw new ProtocolFrameException("A hello field is duplicated.");
                }

                switch (key)
                {
                    case 0:
                        ulong rawVersion = reader.ReadUInt64();
                        if (rawVersion > ushort.MaxValue)
                        {
                            throw new ProtocolFrameException("The protocol version is out of range.");
                        }

                        version = (ushort)rawVersion;
                        break;
                    case 1:
                        string rawDeviceId = reader.ReadTextString();
                        if (rawDeviceId.Length != DeviceId.EncodedLength)
                        {
                            throw new ProtocolFrameException("The device ID length is invalid.");
                        }

                        deviceId = DeviceId.Parse(rawDeviceId);
                        break;
                    case 2:
                        nonce = reader.ReadByteString();
                        if (nonce.Length != 32)
                        {
                            throw new ProtocolFrameException("The hello nonce length is invalid.");
                        }

                        break;
                    case 3:
                        featureBits = reader.ReadUInt64();
                        break;
                    case 4:
                        unixTimeMilliseconds = reader.ReadInt64();
                        break;
                    default:
                        throw new ProtocolFrameException("The hello message contains an unknown field.");
                }
            }

            reader.ReadEndMap();
            if (reader.BytesRemaining != 0)
            {
                throw new ProtocolFrameException("Trailing CBOR data is not allowed.");
            }

            if (version is null || deviceId is null || nonce is null ||
                featureBits is null || unixTimeMilliseconds is null)
            {
                throw new ProtocolFrameException("The hello message is incomplete.");
            }

            return new HelloMessage(
                version.Value,
                deviceId.Value,
                nonce,
                featureBits.Value,
                unixTimeMilliseconds.Value);
        }
        catch (ProtocolFrameException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is CborContentException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new ProtocolFrameException(
                "The hello CBOR payload is malformed.",
                exception);
        }
    }

    private static void RejectDuplicatedTopLevelField(ReadOnlyMemory<byte> payload)
    {
        try
        {
            var reader = new CborReader(payload, CborConformanceMode.Lax);
            int? count = reader.ReadStartMap();
            if (count != 5)
            {
                return;
            }

            var seenKeys = new HashSet<int>();
            for (int index = 0; index < 5; index++)
            {
                int key = reader.ReadInt32();
                if (!seenKeys.Add(key))
                {
                    throw new ProtocolFrameException("A hello field is duplicated.");
                }

                switch (key)
                {
                    case 0:
                        _ = reader.ReadUInt64();
                        break;
                    case 1:
                        _ = reader.ReadTextString();
                        break;
                    case 2:
                        _ = reader.ReadByteString();
                        break;
                    case 3:
                        _ = reader.ReadUInt64();
                        break;
                    case 4:
                        _ = reader.ReadInt64();
                        break;
                    default:
                        return;
                }
            }
        }
        catch (Exception exception) when (
            exception is CborContentException or InvalidOperationException or OverflowException or FormatException)
        {
            return;
        }
    }

    private static int ReadFieldKey(CborReader reader)
    {
        try
        {
            switch (reader.PeekState())
            {
                case CborReaderState.UnsignedInteger:
                    ulong unsignedKey = reader.ReadUInt64();
                    if (unsignedKey > int.MaxValue)
                    {
                        throw new ProtocolFrameException(
                            "The hello message contains an unknown field.");
                    }

                    return (int)unsignedKey;
                case CborReaderState.NegativeInteger:
                    long negativeKey = reader.ReadInt64();
                    if (negativeKey < int.MinValue)
                    {
                        throw new ProtocolFrameException(
                            "The hello message contains an unknown field.");
                    }

                    return (int)negativeKey;
                default:
                    return reader.ReadInt32();
            }
        }
        catch (OverflowException)
        {
            throw new ProtocolFrameException(
                "The hello message contains an unknown field.");
        }
    }
}
