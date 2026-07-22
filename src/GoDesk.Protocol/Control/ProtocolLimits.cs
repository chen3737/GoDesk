namespace GoDesk.Protocol.Control;

public static class ProtocolLimits
{
    public const ushort CurrentVersion = 1;
    public const int ControlHeaderBytes = 8;
    public const int MaxControlPayloadBytes = 64 * 1024;
    public const int MaxTextBytes = 4 * 1024;
    public const int MaxCollectionItems = 1_024;
    public const int MaxCborDepth = 8;
}
