namespace GoDesk.Protocol.Control;

public sealed class ProtocolFrameException : Exception
{
    public ProtocolFrameException()
        : base("The protocol frame is invalid.")
    {
    }

    public ProtocolFrameException(string message)
        : base(message)
    {
    }

    public ProtocolFrameException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
