using GoDesk.Core.Validation;

namespace GoDesk.Foundation.Verification.Core;

public sealed class InputGuardTests
{
    // These approved descriptive xUnit test names intentionally retain underscores.
#pragma warning disable CA1707
    [Fact]
    public void UTF8_limit_counts_bytes_not_UTF16_characters()
    {
        const string InvalidFieldNameMessage =
            "Field name must be 1 to 64 characters and contain only ASCII letters, digits, '_', '.', or '-'.";
        string expectedInvalidFieldNameMessage = new ArgumentException(
            InvalidFieldNameMessage,
            "fieldName").Message;

        InputGuard.Utf8Text("名称", maxBytes: 6, fieldName: "name");
        InputGuard.Utf8Text(string.Empty, maxBytes: 0, fieldName: "name");
        InputGuard.Utf8Text("�", maxBytes: 3, fieldName: "name");
        InputGuard.Utf8Text("😀", maxBytes: 4, fieldName: "name");
        InputGuard.Utf8Text("value", maxBytes: 5, fieldName: "field.Name-1_2");

        GoDeskException exception = Assert.Throws<GoDeskException>(() =>
            InputGuard.Utf8Text("名称", maxBytes: 5, fieldName: "name"));

        Assert.Equal(GoDeskErrorCode.InvalidInput, exception.Code);
        Assert.Equal("Field 'name' must not exceed 5 UTF-8 bytes.", exception.Message);

        GoDeskException emojiException = Assert.Throws<GoDeskException>(() =>
            InputGuard.Utf8Text("😀", maxBytes: 3, fieldName: "name"));
        Assert.Equal(GoDeskErrorCode.InvalidInput, emojiException.Code);
        Assert.Equal("Field 'name' must not exceed 3 UTF-8 bytes.", emojiException.Message);

        GoDeskException highSurrogateException = Assert.Throws<GoDeskException>(() =>
            InputGuard.Utf8Text("\uD800", maxBytes: 3, fieldName: "name"));
        Assert.Equal(GoDeskErrorCode.InvalidInput, highSurrogateException.Code);
        Assert.Equal(
            "Field 'name' must contain valid Unicode text.",
            highSurrogateException.Message);

        GoDeskException lowSurrogateException = Assert.Throws<GoDeskException>(() =>
            InputGuard.Utf8Text("\uDC00", maxBytes: 3, fieldName: "name"));
        Assert.Equal(GoDeskErrorCode.InvalidInput, lowSurrogateException.Code);
        Assert.Equal(
            "Field 'name' must contain valid Unicode text.",
            lowSurrogateException.Message);

        string longAscii = new('a', 100_000);
        GoDeskException longAsciiException = Assert.Throws<GoDeskException>(() =>
            InputGuard.Utf8Text(longAscii, maxBytes: 1, fieldName: "name"));
        Assert.Equal(GoDeskErrorCode.InvalidInput, longAsciiException.Code);
        Assert.Equal("Field 'name' must not exceed 1 UTF-8 bytes.", longAsciiException.Message);

        ArgumentNullException nullException = Assert.Throws<ArgumentNullException>(() =>
            InputGuard.Utf8Text(null!, maxBytes: 5, fieldName: "name"));
        Assert.Equal("value", nullException.ParamName);

        ArgumentOutOfRangeException maximumException = Assert.Throws<ArgumentOutOfRangeException>(() =>
            InputGuard.Utf8Text("value", maxBytes: -1, fieldName: "name"));
        Assert.Equal("maxBytes", maximumException.ParamName);

        ArgumentException emptyFieldNameException = Assert.Throws<ArgumentException>(() =>
            InputGuard.Utf8Text("value", maxBytes: -1, fieldName: string.Empty));
        Assert.Equal("fieldName", emptyFieldNameException.ParamName);
        Assert.Equal(expectedInvalidFieldNameMessage, emptyFieldNameException.Message);

        ArgumentException nullFieldNameException = Assert.Throws<ArgumentException>(() =>
            InputGuard.Utf8Text("value", maxBytes: 5, fieldName: null!));
        Assert.Equal("fieldName", nullFieldNameException.ParamName);
        Assert.Equal(expectedInvalidFieldNameMessage, nullFieldNameException.Message);

        ArgumentException carriageReturnFieldNameException = Assert.Throws<ArgumentException>(() =>
            InputGuard.Utf8Text("value", maxBytes: 5, fieldName: "field\rname"));
        Assert.Equal("fieldName", carriageReturnFieldNameException.ParamName);
        Assert.Equal(expectedInvalidFieldNameMessage, carriageReturnFieldNameException.Message);

        var defaultException = new GoDeskException();
        Assert.Equal(GoDeskErrorCode.InternalFailure, defaultException.Code);
        Assert.Equal("An internal GoDesk failure occurred.", defaultException.Message);

        var safeMessageException = new GoDeskException("A safe message.");
        Assert.Equal(GoDeskErrorCode.InternalFailure, safeMessageException.Code);
        Assert.Equal("A safe message.", safeMessageException.Message);

        var innerException = new InvalidOperationException("Sensitive detail.");
        var wrappedException = new GoDeskException("A safe wrapper.", innerException);
        Assert.Equal(GoDeskErrorCode.InternalFailure, wrappedException.Code);
        Assert.Equal("A safe wrapper.", wrappedException.Message);
        Assert.Same(innerException, wrappedException.InnerException);

        var codedException = new GoDeskException(GoDeskErrorCode.Conflict, "A safe conflict.");
        Assert.Equal(GoDeskErrorCode.Conflict, codedException.Code);
        Assert.Equal("A safe conflict.", codedException.Message);
        Assert.False(typeof(GoDeskException).GetProperty(nameof(GoDeskException.Code))!.CanWrite);

        Assert.Equal(1, (int)GoDeskErrorCode.InvalidInput);
        Assert.Equal(2, (int)GoDeskErrorCode.ProtocolViolation);
        Assert.Equal(3, (int)GoDeskErrorCode.AuthenticationFailed);
        Assert.Equal(4, (int)GoDeskErrorCode.PermissionDenied);
        Assert.Equal(5, (int)GoDeskErrorCode.NotFound);
        Assert.Equal(6, (int)GoDeskErrorCode.Busy);
        Assert.Equal(7, (int)GoDeskErrorCode.Timeout);
        Assert.Equal(8, (int)GoDeskErrorCode.Unavailable);
        Assert.Equal(9, (int)GoDeskErrorCode.Conflict);
        Assert.Equal(10, (int)GoDeskErrorCode.InternalFailure);
    }

    [Fact]
    public void Collection_limit_accepts_the_boundary_and_rejects_one_more()
    {
        const string InvalidFieldNameMessage =
            "Field name must be 1 to 64 characters and contain only ASCII letters, digits, '_', '.', or '-'.";
        string expectedInvalidFieldNameMessage = new ArgumentException(
            InvalidFieldNameMessage,
            "fieldName").Message;

        InputGuard.CollectionCount(10, maxCount: 10, fieldName: "items");
        InputGuard.CollectionCount(0, maxCount: 0, fieldName: "items");
        InputGuard.CollectionCount(1, maxCount: 1, fieldName: "field.Name-1_2");

        GoDeskException negativeException = Assert.Throws<GoDeskException>(() =>
            InputGuard.CollectionCount(-1, maxCount: 10, fieldName: "items"));
        Assert.Equal(GoDeskErrorCode.InvalidInput, negativeException.Code);
        Assert.Equal(
            "Field 'items' must contain between 0 and 10 items.",
            negativeException.Message);

        GoDeskException oversizedException = Assert.Throws<GoDeskException>(() =>
            InputGuard.CollectionCount(11, maxCount: 10, fieldName: "items"));
        Assert.Equal(GoDeskErrorCode.InvalidInput, oversizedException.Code);
        Assert.Equal(
            "Field 'items' must contain between 0 and 10 items.",
            oversizedException.Message);

        ArgumentOutOfRangeException maximumException = Assert.Throws<ArgumentOutOfRangeException>(() =>
            InputGuard.CollectionCount(0, maxCount: -1, fieldName: "items"));
        Assert.Equal("maxCount", maximumException.ParamName);

        ArgumentException lineFeedFieldNameException = Assert.Throws<ArgumentException>(() =>
            InputGuard.CollectionCount(0, maxCount: -1, fieldName: "field\nname"));
        Assert.Equal("fieldName", lineFeedFieldNameException.ParamName);
        Assert.Equal(expectedInvalidFieldNameMessage, lineFeedFieldNameException.Message);

        ArgumentException quotedFieldNameException = Assert.Throws<ArgumentException>(() =>
            InputGuard.CollectionCount(0, maxCount: 0, fieldName: "field'name"));
        Assert.Equal("fieldName", quotedFieldNameException.ParamName);
        Assert.Equal(expectedInvalidFieldNameMessage, quotedFieldNameException.Message);
    }

    [Fact]
    public void Byte_limit_rejects_negative_and_oversized_values()
    {
        const string InvalidFieldNameMessage =
            "Field name must be 1 to 64 characters and contain only ASCII letters, digits, '_', '.', or '-'.";
        string expectedInvalidFieldNameMessage = new ArgumentException(
            InvalidFieldNameMessage,
            "fieldName").Message;

        InputGuard.ByteLength(10, maxLength: 10, fieldName: "payload");
        InputGuard.ByteLength(0, maxLength: 0, fieldName: "payload");
        InputGuard.ByteLength(1, maxLength: 1, fieldName: "field.Name-1_2");

        GoDeskException negativeException = Assert.Throws<GoDeskException>(() =>
            InputGuard.ByteLength(-1, maxLength: 10, fieldName: "payload"));
        Assert.Equal(GoDeskErrorCode.InvalidInput, negativeException.Code);
        Assert.Equal(
            "Field 'payload' must contain between 0 and 10 bytes.",
            negativeException.Message);

        GoDeskException oversizedException = Assert.Throws<GoDeskException>(() =>
            InputGuard.ByteLength(11, maxLength: 10, fieldName: "payload"));
        Assert.Equal(GoDeskErrorCode.InvalidInput, oversizedException.Code);
        Assert.Equal(
            "Field 'payload' must contain between 0 and 10 bytes.",
            oversizedException.Message);

        ArgumentOutOfRangeException maximumException = Assert.Throws<ArgumentOutOfRangeException>(() =>
            InputGuard.ByteLength(0, maxLength: -1, fieldName: "payload"));
        Assert.Equal("maxLength", maximumException.ParamName);

        ArgumentException controlFieldNameException = Assert.Throws<ArgumentException>(() =>
            InputGuard.ByteLength(0, maxLength: -1, fieldName: "field\u0001name"));
        Assert.Equal("fieldName", controlFieldNameException.ParamName);
        Assert.Equal(expectedInvalidFieldNameMessage, controlFieldNameException.Message);

        string oversizedFieldName = new('a', 65);
        ArgumentException oversizedFieldNameException = Assert.Throws<ArgumentException>(() =>
            InputGuard.ByteLength(0, maxLength: 0, fieldName: oversizedFieldName));
        Assert.Equal("fieldName", oversizedFieldNameException.ParamName);
        Assert.Equal(expectedInvalidFieldNameMessage, oversizedFieldNameException.Message);
    }
#pragma warning restore CA1707
}
