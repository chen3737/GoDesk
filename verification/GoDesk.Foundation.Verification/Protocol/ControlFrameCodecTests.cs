using System.Buffers.Binary;
using GoDesk.Protocol.Control;

namespace GoDesk.Foundation.Verification.Protocol;

public sealed class ControlFrameCodecTests
{
    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Frame_round_trips_with_exact_payload()
    {
        Assert.Equal((ushort)1, ProtocolLimits.CurrentVersion);
        Assert.Equal(8, ProtocolLimits.ControlHeaderBytes);
        Assert.Equal(64 * 1024, ProtocolLimits.MaxControlPayloadBytes);
        Assert.Equal(4 * 1024, ProtocolLimits.MaxTextBytes);
        Assert.Equal(1024, ProtocolLimits.MaxCollectionItems);
        Assert.Equal(8, ProtocolLimits.MaxCborDepth);
        Assert.Equal((ushort)1, (ushort)ControlMessageType.Hello);
        Assert.Equal((ushort)2, (ushort)ControlMessageType.Challenge);
        Assert.Equal((ushort)3, (ushort)ControlMessageType.Pairing);
        Assert.Equal((ushort)4, (ushort)ControlMessageType.PermissionState);
        Assert.Equal((ushort)5, (ushort)ControlMessageType.Error);

        byte[] payload = [0x10, 0x20, 0x30, 0x40];
        byte[] encoded = ControlFrameCodec.Encode(ControlMessageType.Hello, payload);

        Assert.Equal(
            new byte[] { 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x04, 0x10, 0x20, 0x30, 0x40 },
            encoded);

        payload[0] = 0xFF;
        Assert.Equal(0x10, encoded[ProtocolLimits.ControlHeaderBytes]);

        ControlFrame decoded = ControlFrameCodec.Decode(encoded);
        ControlFrame separatelyDecoded = ControlFrameCodec.Decode(encoded);

        Assert.Equal(decoded, separatelyDecoded);
        Assert.Equal(decoded.GetHashCode(), separatelyDecoded.GetHashCode());

        ControlFrame differentPayload = new(
            ProtocolLimits.CurrentVersion,
            ControlMessageType.Hello,
            new byte[] { 0x10, 0x20, 0x30, 0x41 });
        Assert.NotEqual(decoded, differentPayload);

        byte[] constructorPayload = [0xA0, 0xB0];
        ReadOnlyMemory<byte> constructorMemory = constructorPayload;
        ControlFrame constructed = new(
            Version: ProtocolLimits.CurrentVersion,
            MessageType: ControlMessageType.Pairing,
            Payload: constructorMemory);
        constructorPayload[0] = 0xFF;
        Assert.Equal(new byte[] { 0xA0, 0xB0 }, constructed.Payload.ToArray());

        (ushort deconstructedVersion, ControlMessageType deconstructedType, ReadOnlyMemory<byte> deconstructedPayload) =
            decoded;
        Assert.Equal(ProtocolLimits.CurrentVersion, deconstructedVersion);
        Assert.Equal(ControlMessageType.Hello, deconstructedType);
        Assert.Equal(new byte[] { 0x10, 0x20, 0x30, 0x40 }, deconstructedPayload.ToArray());

        ControlFrame metadataChanged = constructed with
        {
            Version = ProtocolLimits.CurrentVersion + 1,
            MessageType = ControlMessageType.Error,
        };
        Assert.Equal(ProtocolLimits.CurrentVersion + 1, metadataChanged.Version);
        Assert.Equal(ControlMessageType.Error, metadataChanged.MessageType);
        Assert.Equal(constructed.Payload, metadataChanged.Payload);

        byte[] replacementPayload = [0xC0, 0xD0];
        ReadOnlyMemory<byte> replacementMemory = replacementPayload;
        ControlFrame payloadChanged = constructed with { Payload = replacementMemory };
        replacementPayload[0] = 0xFF;
        Assert.Equal(new byte[] { 0xC0, 0xD0 }, payloadChanged.Payload.ToArray());

        encoded[ProtocolLimits.ControlHeaderBytes] = 0xEE;

        Assert.Equal(ProtocolLimits.CurrentVersion, decoded.Version);
        Assert.Equal(ControlMessageType.Hello, decoded.MessageType);
        Assert.Equal(new byte[] { 0x10, 0x20, 0x30, 0x40 }, decoded.Payload.ToArray());

        byte[] emptyEncoded = ControlFrameCodec.Encode(ControlMessageType.Error, ReadOnlySpan<byte>.Empty);
        ControlFrame emptyDecoded = ControlFrameCodec.Decode(emptyEncoded);

        Assert.Equal(new byte[] { 0x00, 0x01, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00 }, emptyEncoded);
        Assert.Empty(emptyDecoded.Payload.ToArray());
    }
#pragma warning restore CA1707

    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Oversized_payload_is_rejected_before_encoding()
    {
        byte[] maximumPayload = new byte[ProtocolLimits.MaxControlPayloadBytes];
        byte[] maximumFrame = ControlFrameCodec.Encode(ControlMessageType.Hello, maximumPayload);
        Assert.Equal(ProtocolLimits.ControlHeaderBytes + ProtocolLimits.MaxControlPayloadBytes, maximumFrame.Length);
        ControlFrame maximumDecoded = ControlFrameCodec.Decode(maximumFrame);
        Assert.Equal(ProtocolLimits.MaxControlPayloadBytes, maximumDecoded.Payload.Length);
        Assert.Equal(maximumPayload, maximumDecoded.Payload.ToArray());

        byte[] oversizedPayload = new byte[ProtocolLimits.MaxControlPayloadBytes + 1];
        ProtocolFrameException oversizedException = Assert.Throws<ProtocolFrameException>(() =>
            ControlFrameCodec.Encode(ControlMessageType.Hello, oversizedPayload));
        Assert.Equal("The control payload exceeds 64 KiB.", oversizedException.Message);

        ProtocolFrameException unknownTypeException = Assert.Throws<ProtocolFrameException>(() =>
            ControlFrameCodec.Encode((ControlMessageType)0, ReadOnlySpan<byte>.Empty));
        Assert.Equal("The control message type is unknown.", unknownTypeException.Message);

        byte[] advertisedOversizedFrame = new byte[ProtocolLimits.ControlHeaderBytes];
        BinaryPrimitives.WriteUInt16BigEndian(advertisedOversizedFrame, ProtocolLimits.CurrentVersion);
        BinaryPrimitives.WriteUInt16BigEndian(advertisedOversizedFrame.AsSpan(2), (ushort)ControlMessageType.Hello);
        BinaryPrimitives.WriteUInt32BigEndian(
            advertisedOversizedFrame.AsSpan(4),
            (uint)ProtocolLimits.MaxControlPayloadBytes + 1U);

        ProtocolFrameException advertisedException = Assert.Throws<ProtocolFrameException>(
            () => ControlFrameCodec.Decode(advertisedOversizedFrame));
        Assert.Equal("The advertised payload exceeds 64 KiB.", advertisedException.Message);

        byte[] maximalAdvertisedFrame = new byte[ProtocolLimits.ControlHeaderBytes];
        BinaryPrimitives.WriteUInt16BigEndian(maximalAdvertisedFrame, ProtocolLimits.CurrentVersion);
        BinaryPrimitives.WriteUInt16BigEndian(maximalAdvertisedFrame.AsSpan(2), (ushort)ControlMessageType.Hello);
        BinaryPrimitives.WriteUInt32BigEndian(maximalAdvertisedFrame.AsSpan(4), uint.MaxValue);

        ProtocolFrameException maximalAdvertisedException = Assert.Throws<ProtocolFrameException>(
            () => ControlFrameCodec.Decode(maximalAdvertisedFrame));
        Assert.Equal("The advertised payload exceeds 64 KiB.", maximalAdvertisedException.Message);
    }
#pragma warning restore CA1707

    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Truncated_frame_is_rejected()
    {
        byte[] validFrame = ControlFrameCodec.Encode(ControlMessageType.Challenge, new byte[] { 0x01, 0x02 });

        ProtocolFrameException headerException = Assert.Throws<ProtocolFrameException>(
            () => ControlFrameCodec.Decode(new byte[ProtocolLimits.ControlHeaderBytes - 1]));
        Assert.Equal("The control frame header is truncated.", headerException.Message);

        ProtocolFrameException missingPayloadException = Assert.Throws<ProtocolFrameException>(
            () => ControlFrameCodec.Decode(validFrame.AsSpan(0, validFrame.Length - 1)));
        Assert.Equal("The control frame length does not match its header.", missingPayloadException.Message);

        byte[] frameWithTrailingByte = new byte[validFrame.Length + 1];
        validFrame.CopyTo(frameWithTrailingByte, 0);
        ProtocolFrameException trailingPayloadException = Assert.Throws<ProtocolFrameException>(
            () => ControlFrameCodec.Decode(frameWithTrailingByte));
        Assert.Equal("The control frame length does not match its header.", trailingPayloadException.Message);

        byte[] unsupportedVersionFrame = ControlFrameCodec.Encode(ControlMessageType.Hello, ReadOnlySpan<byte>.Empty);
        BinaryPrimitives.WriteUInt16BigEndian(unsupportedVersionFrame, ProtocolLimits.CurrentVersion + 1);
        ProtocolFrameException versionException = Assert.Throws<ProtocolFrameException>(
            () => ControlFrameCodec.Decode(unsupportedVersionFrame));
        Assert.Equal("The protocol version is unsupported.", versionException.Message);
    }
#pragma warning restore CA1707

    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Unknown_message_type_is_rejected()
    {
        byte[] unknownTypeFrame = new byte[ProtocolLimits.ControlHeaderBytes];
        BinaryPrimitives.WriteUInt16BigEndian(unknownTypeFrame, ProtocolLimits.CurrentVersion);
        BinaryPrimitives.WriteUInt16BigEndian(unknownTypeFrame.AsSpan(2), ushort.MaxValue);

        ProtocolFrameException exception = Assert.Throws<ProtocolFrameException>(
            () => ControlFrameCodec.Decode(unknownTypeFrame));
        Assert.Equal("The control message type is unknown.", exception.Message);

        ProtocolFrameException defaultException = new();
        ProtocolFrameException customException = new("custom");
        InvalidOperationException innerException = new("inner");
        ProtocolFrameException wrappedException = new("wrapped", innerException);

        Assert.Equal("The protocol frame is invalid.", defaultException.Message);
        Assert.Equal("custom", customException.Message);
        Assert.Equal("wrapped", wrappedException.Message);
        Assert.Same(innerException, wrappedException.InnerException);
    }
#pragma warning restore CA1707
}
