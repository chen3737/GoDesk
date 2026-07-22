using System.Text;

namespace GoDesk.Core.Validation;

public static class InputGuard
{
    private const string InvalidFieldNameMessage =
        "Field name must be 1 to 64 characters and contain only ASCII letters, digits, '_', '.', or '-'.";

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static void Utf8Text(string value, int maxBytes, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateFieldName(fieldName);
        ValidateMaximum(maxBytes, nameof(maxBytes));

        if (value.Length > maxBytes)
        {
            throw Invalid(fieldName, $"must not exceed {maxBytes} UTF-8 bytes");
        }

        try
        {
            if (StrictUtf8.GetByteCount(value) > maxBytes)
            {
                throw Invalid(fieldName, $"must not exceed {maxBytes} UTF-8 bytes");
            }
        }
        catch (EncoderFallbackException)
        {
            throw Invalid(fieldName, "must contain valid Unicode text");
        }
    }

    public static void CollectionCount(int count, int maxCount, string fieldName)
    {
        ValidateFieldName(fieldName);
        ValidateMaximum(maxCount, nameof(maxCount));
        if (count < 0 || count > maxCount)
        {
            throw Invalid(fieldName, $"must contain between 0 and {maxCount} items");
        }
    }

    public static void ByteLength(long length, long maxLength, string fieldName)
    {
        ValidateFieldName(fieldName);
        ValidateMaximum(maxLength, nameof(maxLength));
        if (length < 0 || length > maxLength)
        {
            throw Invalid(fieldName, $"must contain between 0 and {maxLength} bytes");
        }
    }

    private static void ValidateMaximum(long maximum, string parameterName)
    {
        if (maximum < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateFieldName(string? fieldName)
    {
        if (string.IsNullOrEmpty(fieldName) || fieldName.Length > 64)
        {
            throw new ArgumentException(InvalidFieldNameMessage, nameof(fieldName));
        }

        foreach (char character in fieldName)
        {
            bool isAsciiLetter =
                (character >= 'A' && character <= 'Z') ||
                (character >= 'a' && character <= 'z');
            bool isAsciiDigit = character >= '0' && character <= '9';
            bool isSeparator = character is '_' or '.' or '-';
            if (!isAsciiLetter && !isAsciiDigit && !isSeparator)
            {
                throw new ArgumentException(InvalidFieldNameMessage, nameof(fieldName));
            }
        }
    }

    private static GoDeskException Invalid(string fieldName, string rule) =>
        new(GoDeskErrorCode.InvalidInput, $"Field '{fieldName}' {rule}.");
}
