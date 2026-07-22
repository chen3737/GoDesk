using System.Security.Cryptography;

namespace GoDesk.Core.Identity;

public readonly record struct DeviceId
{
    private const string UninitializedMessage = "An uninitialized device ID has no value.";

    private readonly string? _value;

    public const int EncodedLength = 52;

    private DeviceId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? throw new InvalidOperationException(UninitializedMessage);

    // Hashes the exact, complete DER SPKI bytes without normalization. The caller must first
    // validate the key and algorithm.
    public static DeviceId FromSubjectPublicKeyInfo(ReadOnlySpan<byte> subjectPublicKeyInfo)
    {
        if (subjectPublicKeyInfo.IsEmpty)
        {
            throw new ArgumentException(
                "The public key cannot be empty.",
                nameof(subjectPublicKeyInfo));
        }

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.HashData(subjectPublicKeyInfo, hash);
        return new DeviceId(Base32NoPadding.Encode(hash));
    }

    public static DeviceId Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length != EncodedLength ||
            !IsBase32(value) ||
            (value[^1] != 'A' && value[^1] != 'Q'))
        {
            throw new FormatException("A device ID must be 52 uppercase Base32 characters.");
        }

        return new DeviceId(value);
    }

    public override string ToString() => Value;

    private static bool IsBase32(string value)
    {
        foreach (char character in value)
        {
            if ((character < 'A' || character > 'Z') &&
                (character < '2' || character > '7'))
            {
                return false;
            }
        }

        return true;
    }
}
