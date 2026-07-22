using GoDesk.Core.Identity;

namespace GoDesk.Protocol.Handshake;

public sealed record HelloMessage
{
    public HelloMessage(
        ushort protocolVersion,
        DeviceId deviceId,
        ReadOnlySpan<byte> nonce,
        ulong featureBits,
        long unixTimeMilliseconds)
    {
        if (nonce.Length != 32)
        {
            throw new ArgumentException(
                "A hello nonce must contain exactly 32 bytes.",
                nameof(nonce));
        }

        if (deviceId == default)
        {
            throw new ArgumentException(
                "A hello message requires a device ID.",
                nameof(deviceId));
        }

        ProtocolVersion = protocolVersion;
        DeviceId = deviceId;
        Nonce = nonce.ToArray();
        FeatureBits = featureBits;
        UnixTimeMilliseconds = unixTimeMilliseconds;
    }

    public ushort ProtocolVersion { get; }

    public DeviceId DeviceId { get; }

    public ReadOnlyMemory<byte> Nonce { get; }

    public ulong FeatureBits { get; }

    public long UnixTimeMilliseconds { get; }

    public bool Equals(HelloMessage? other)
    {
        return other is not null &&
            ProtocolVersion == other.ProtocolVersion &&
            DeviceId == other.DeviceId &&
            Nonce.Span.SequenceEqual(other.Nonce.Span) &&
            FeatureBits == other.FeatureBits &&
            UnixTimeMilliseconds == other.UnixTimeMilliseconds;
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(ProtocolVersion);
        hashCode.Add(DeviceId);
        foreach (byte value in Nonce.Span)
        {
            hashCode.Add(value);
        }

        hashCode.Add(FeatureBits);
        hashCode.Add(UnixTimeMilliseconds);
        return hashCode.ToHashCode();
    }
}
