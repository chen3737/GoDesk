namespace GoDesk.Core.Validation;

public sealed class GoDeskException : Exception
{
    public GoDeskException()
        : this(GoDeskErrorCode.InternalFailure, "An internal GoDesk failure occurred.")
    {
    }

    public GoDeskException(string safeMessage)
        : this(GoDeskErrorCode.InternalFailure, safeMessage)
    {
    }

    public GoDeskException(string safeMessage, Exception innerException)
        : base(safeMessage, innerException)
    {
        Code = GoDeskErrorCode.InternalFailure;
    }

    public GoDeskException(GoDeskErrorCode code, string safeMessage)
        : base(safeMessage)
    {
        Code = code;
    }

    public GoDeskErrorCode Code { get; }
}
