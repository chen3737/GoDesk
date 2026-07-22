namespace GoDesk.Protocol.Control;

public sealed record ControlFrame
{
    private ReadOnlyMemory<byte> payload;

    public ControlFrame(
        ushort Version,
        ControlMessageType MessageType,
        ReadOnlyMemory<byte> Payload)
        : this(Version, MessageType, Payload.ToArray())
    {
    }

    private ControlFrame(
        ushort Version,
        ControlMessageType MessageType,
        byte[] ownedPayload)
    {
        this.Version = Version;
        this.MessageType = MessageType;
        payload = ownedPayload;
    }

    public ushort Version { get; init; }

    public ControlMessageType MessageType { get; init; }

    public ReadOnlyMemory<byte> Payload
    {
        get => payload;
        init => payload = value.ToArray();
    }

    internal static ControlFrame FromOwnedPayload(
        ushort version,
        ControlMessageType messageType,
        byte[] ownedPayload)
    {
        ArgumentNullException.ThrowIfNull(ownedPayload);
        return new ControlFrame(version, messageType, ownedPayload);
    }

    public void Deconstruct(
        out ushort Version,
        out ControlMessageType MessageType,
        out ReadOnlyMemory<byte> Payload)
    {
        Version = this.Version;
        MessageType = this.MessageType;
        Payload = this.Payload;
    }

    public bool Equals(ControlFrame? other)
    {
        return other is not null &&
            Version == other.Version &&
            MessageType == other.MessageType &&
            Payload.Span.SequenceEqual(other.Payload.Span);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Version);
        hashCode.Add(MessageType);
        foreach (byte value in Payload.Span)
        {
            hashCode.Add(value);
        }

        return hashCode.ToHashCode();
    }
}
