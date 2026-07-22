using System.Formats.Cbor;
using GoDesk.Core.Identity;
using GoDesk.Protocol.Control;
using GoDesk.Protocol.Handshake;

namespace GoDesk.Foundation.Verification.Protocol;

public sealed class HelloMessageCodecTests
{
    private const string DeviceIdText =
        "ECERQOAXSQ24VDLBYHRJMXPVB57XHUCYHYYITCHZSQGG3KRJSA7Q";

    private static readonly DeviceId SampleDeviceId = DeviceId.Parse(DeviceIdText);

    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Hello_round_trips_without_losing_fields()
    {
        byte[] inputNonce = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var original = new HelloMessage(
            ProtocolLimits.CurrentVersion,
            SampleDeviceId,
            inputNonce,
            featureBits: 0x15,
            unixTimeMilliseconds: 1_700_000_000_000);
        var equalMessage = new HelloMessage(
            ProtocolLimits.CurrentVersion,
            SampleDeviceId,
            Enumerable.Range(0, 32).Select(value => (byte)value).ToArray(),
            featureBits: 0x15,
            unixTimeMilliseconds: 1_700_000_000_000);
        byte[] differentNonce = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        differentNonce[^1] = 0xFF;
        var differentMessage = new HelloMessage(
            ProtocolLimits.CurrentVersion,
            SampleDeviceId,
            differentNonce,
            featureBits: 0x15,
            unixTimeMilliseconds: 1_700_000_000_000);

        inputNonce[0] = 0xFF;
        Assert.Equal(0x00, original.Nonce.Span[0]);
        Assert.Equal(original, equalMessage);
        Assert.Equal(original.GetHashCode(), equalMessage.GetHashCode());
        Assert.NotEqual(original, differentMessage);

        byte[] encoded = HelloMessageCodec.Encode(original);
        byte[] expected = Convert.FromHexString(
            "A50001017834" +
            "45434552514F41585351323456444C425948524A4D5850564235375848554359" +
            "485959495443485A53514747334B524A53413751" +
            "025820000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F" +
            "0315041B0000018BCFE56800");
        Assert.Equal(expected, encoded);

        HelloMessage decoded = HelloMessageCodec.Decode(encoded);
        encoded[61] = 0xEE;

        Assert.Equal(original.ProtocolVersion, decoded.ProtocolVersion);
        Assert.Equal(original.DeviceId, decoded.DeviceId);
        Assert.Equal(original.Nonce.ToArray(), decoded.Nonce.ToArray());
        Assert.Equal(original.FeatureBits, decoded.FeatureBits);
        Assert.Equal(original.UnixTimeMilliseconds, decoded.UnixTimeMilliseconds);
        Assert.Equal(0x00, decoded.Nonce.Span[0]);
        Assert.Equal(original, decoded);
        Assert.Equal(original.GetHashCode(), decoded.GetHashCode());
        Assert.Throws<ArgumentNullException>(() => HelloMessageCodec.Encode(null!));
    }
#pragma warning restore CA1707

    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Nonce_must_be_exactly_32_bytes()
    {
        foreach (int invalidLength in new[] { 31, 33 })
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => new HelloMessage(
                ProtocolLimits.CurrentVersion,
                SampleDeviceId,
                new byte[invalidLength],
                0,
                0));

            Assert.Equal("nonce", exception.ParamName);
            Assert.StartsWith(
                "A hello nonce must contain exactly 32 bytes.",
                exception.Message,
                StringComparison.Ordinal);
        }

        ArgumentException deviceIdException = Assert.Throws<ArgumentException>(() => new HelloMessage(
            ProtocolLimits.CurrentVersion,
            default,
            new byte[32],
            0,
            0));

        Assert.Equal("deviceId", deviceIdException.ParamName);
        Assert.StartsWith(
            "A hello message requires a device ID.",
            deviceIdException.Message,
            StringComparison.Ordinal);
    }
#pragma warning restore CA1707

    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Trailing_CBOR_data_is_rejected()
    {
        byte[] validPayload = WriteMap(5, WriteValidFields);
        byte[] oversizedPayload = new byte[ProtocolLimits.MaxControlPayloadBytes + 1];
        byte[] truncatedPayload = validPayload[..^1];
        byte[] malformedPayload = [0xFF];
        byte[] indefiniteMap = WriteMap(null, WriteValidFields);
        byte[] nonCanonicalInteger = [validPayload[0], 0x18, 0x00, .. validPayload.AsSpan(2)];
        byte[] fourFieldMap = WriteMap(4, writer =>
        {
            WriteVersion(writer);
            WriteDeviceId(writer, DeviceIdText);
            WriteNonce(writer, new byte[32]);
            WriteFeatureBits(writer);
        });
        byte[] sixFieldMap = WriteMap(6, writer =>
        {
            WriteValidFields(writer);
            writer.WriteInt32(5);
            writer.WriteInt32(0);
        });
        byte[] duplicatedFieldMap = WriteMap(5, writer =>
        {
            WriteVersion(writer);
            WriteDeviceId(writer, DeviceIdText);
            WriteNonce(writer, new byte[32]);
            WriteFeatureBits(writer);
            WriteFeatureBits(writer);
        });
        byte[] unknownFieldMap = WriteMap(5, writer =>
        {
            WriteVersion(writer);
            WriteDeviceId(writer, DeviceIdText);
            WriteNonce(writer, new byte[32]);
            WriteFeatureBits(writer);
            writer.WriteInt32(5);
            writer.WriteInt32(0);
        });
        byte[] unknownBeforeDuplicateMap = WriteMap(5, writer =>
        {
            writer.WriteInt32(5);
            writer.WriteInt32(0);
            WriteVersion(writer);
            WriteVersion(writer);
            WriteDeviceId(writer, DeviceIdText);
            WriteNonce(writer, new byte[32]);
        });
        byte[] largeUnknownFieldMap = WriteMap(5, writer =>
        {
            WriteVersion(writer);
            WriteDeviceId(writer, DeviceIdText);
            WriteNonce(writer, new byte[32]);
            WriteFeatureBits(writer);
            writer.WriteUInt64((ulong)int.MaxValue + 1);
            writer.WriteInt32(0);
        });
        byte[] versionOutOfRangeMap = WriteMap(5, writer =>
        {
            writer.WriteInt32(0);
            writer.WriteUInt64((ulong)ushort.MaxValue + 1);
            WriteDeviceId(writer, DeviceIdText);
            WriteNonce(writer, new byte[32]);
            WriteFeatureBits(writer);
            WriteUnixTime(writer);
        });
        byte[] shortDeviceIdMap = WriteMap(5, writer =>
        {
            WriteVersion(writer);
            WriteDeviceId(writer, new string('A', DeviceId.EncodedLength - 1));
            WriteNonce(writer, new byte[32]);
            WriteFeatureBits(writer);
            WriteUnixTime(writer);
        });
        byte[] invalidCanonicalDeviceIdMap = WriteMap(5, writer =>
        {
            WriteVersion(writer);
            WriteDeviceId(writer, string.Concat(new string('A', DeviceId.EncodedLength - 1), "B"));
            WriteNonce(writer, new byte[32]);
            WriteFeatureBits(writer);
            WriteUnixTime(writer);
        });
        byte[] shortNonceMap = WriteMap(5, writer =>
        {
            WriteVersion(writer);
            WriteDeviceId(writer, DeviceIdText);
            WriteNonce(writer, new byte[31]);
            WriteFeatureBits(writer);
            WriteUnixTime(writer);
        });
        byte[] longNonceMap = WriteMap(5, writer =>
        {
            WriteVersion(writer);
            WriteDeviceId(writer, DeviceIdText);
            WriteNonce(writer, new byte[33]);
            WriteFeatureBits(writer);
            WriteUnixTime(writer);
        });
        byte[] timeOutOfRangeMap = WriteMap(5, writer =>
        {
            WriteVersion(writer);
            WriteDeviceId(writer, DeviceIdText);
            WriteNonce(writer, new byte[32]);
            WriteFeatureBits(writer);
            writer.WriteInt32(4);
            writer.WriteUInt64((ulong)long.MaxValue + 1);
        });
        byte[] wrongFieldTypeMap = WriteMap(5, writer =>
        {
            writer.WriteInt32(0);
            writer.WriteTextString("not a version");
            WriteDeviceId(writer, DeviceIdText);
            WriteNonce(writer, new byte[32]);
            WriteFeatureBits(writer);
            WriteUnixTime(writer);
        });
        byte[] trailingPayload = [.. validPayload, 0x00];
        byte[] deeplyNestedArrayPayload = new byte[ProtocolLimits.MaxControlPayloadBytes];
        deeplyNestedArrayPayload[0] = 0xA5;
        deeplyNestedArrayPayload[1] = 0x00;
        deeplyNestedArrayPayload.AsSpan(2, deeplyNestedArrayPayload.Length - 3).Fill(0x81);
        deeplyNestedArrayPayload[^1] = 0x00;

        AssertProtocolError(
            oversizedPayload,
            "The hello payload exceeds 64 KiB.");
        AssertProtocolError(truncatedPayload, "The hello CBOR payload is malformed.");
        AssertProtocolError(malformedPayload, "The hello CBOR payload is malformed.");
        AssertProtocolError(indefiniteMap, "The hello CBOR payload is malformed.");
        AssertProtocolError(nonCanonicalInteger, "The hello CBOR payload is malformed.");
        AssertProtocolError(fourFieldMap, "A hello message must contain five fields.");
        AssertProtocolError(sixFieldMap, "A hello message must contain five fields.");
        AssertProtocolError(duplicatedFieldMap, "A hello field is duplicated.");
        AssertProtocolError(unknownFieldMap, "The hello message contains an unknown field.");
        AssertProtocolError(unknownBeforeDuplicateMap, "The hello message contains an unknown field.");
        AssertProtocolError(largeUnknownFieldMap, "The hello message contains an unknown field.");
        AssertProtocolError(versionOutOfRangeMap, "The protocol version is out of range.");
        AssertProtocolError(shortDeviceIdMap, "The device ID length is invalid.");
        AssertProtocolError(invalidCanonicalDeviceIdMap, "The hello CBOR payload is malformed.");
        AssertProtocolError(shortNonceMap, "The hello nonce length is invalid.");
        AssertProtocolError(longNonceMap, "The hello nonce length is invalid.");
        AssertProtocolError(timeOutOfRangeMap, "The hello CBOR payload is malformed.");
        AssertProtocolError(wrongFieldTypeMap, "The hello CBOR payload is malformed.");
        AssertProtocolError(trailingPayload, "Trailing CBOR data is not allowed.");
        AssertProtocolError(deeplyNestedArrayPayload, "The hello CBOR payload is malformed.");
    }
#pragma warning restore CA1707

    private static byte[] WriteMap(int? length, Action<CborWriter> writeFields)
    {
        var writer = new CborWriter(CborConformanceMode.Lax);
        writer.WriteStartMap(length);
        writeFields(writer);
        writer.WriteEndMap();
        return writer.Encode();
    }

    private static void WriteValidFields(CborWriter writer)
    {
        WriteVersion(writer);
        WriteDeviceId(writer, DeviceIdText);
        WriteNonce(writer, new byte[32]);
        WriteFeatureBits(writer);
        WriteUnixTime(writer);
    }

    private static void WriteVersion(CborWriter writer)
    {
        writer.WriteInt32(0);
        writer.WriteUInt64(ProtocolLimits.CurrentVersion);
    }

    private static void WriteDeviceId(CborWriter writer, string value)
    {
        writer.WriteInt32(1);
        writer.WriteTextString(value);
    }

    private static void WriteNonce(CborWriter writer, byte[] value)
    {
        writer.WriteInt32(2);
        writer.WriteByteString(value);
    }

    private static void WriteFeatureBits(CborWriter writer)
    {
        writer.WriteInt32(3);
        writer.WriteUInt64(0);
    }

    private static void WriteUnixTime(CborWriter writer)
    {
        writer.WriteInt32(4);
        writer.WriteInt64(0);
    }

    private static void AssertProtocolError(byte[] payload, string expectedMessage)
    {
        ProtocolFrameException exception = Assert.Throws<ProtocolFrameException>(
            () => HelloMessageCodec.Decode(payload));
        Assert.Equal(expectedMessage, exception.Message);
        Assert.DoesNotContain(DeviceIdText, exception.ToString(), StringComparison.Ordinal);
    }
}
