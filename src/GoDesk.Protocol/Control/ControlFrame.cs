namespace GoDesk.Protocol.Control;

public sealed record ControlFrame(
    ushort Version,
    ControlMessageType MessageType,
    ReadOnlyMemory<byte> Payload);
