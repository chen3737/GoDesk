namespace GoDesk.Core.Validation;

public enum GoDeskErrorCode
{
    InvalidInput = 1,
    ProtocolViolation = 2,
    AuthenticationFailed = 3,
    PermissionDenied = 4,
    NotFound = 5,
    Busy = 6,
    Timeout = 7,
    Unavailable = 8,
    Conflict = 9,
    InternalFailure = 10,
}
