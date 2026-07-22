using System.Text;
using GoDesk.Core.Identity;

namespace GoDesk.Foundation.Verification.Core;

public sealed class DeviceIdTests
{
    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Base32_matches_the_RFC_4648_foo_vector()
    {
        (string PlainText, string Encoded)[] vectors =
        [
            (string.Empty, string.Empty),
            ("f", "MY"),
            ("fo", "MZXQ"),
            ("foo", "MZXW6"),
            ("foob", "MZXW6YQ"),
            ("fooba", "MZXW6YTB"),
            ("foobar", "MZXW6YTBOI"),
        ];

        foreach ((string plainText, string encoded) in vectors)
        {
            Assert.Equal(encoded, Base32NoPadding.Encode(Encoding.ASCII.GetBytes(plainText)));
        }
    }
#pragma warning restore CA1707

    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Device_id_is_deterministic_uppercase_and_52_characters()
    {
        const string SubjectPublicKeyInfoBase64 =
            "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEmdy8a+OM10so+18GhBJWWYe70/" +
            "ZUaKS4c+/mLoyaU6nf6IflVzJbiHv24qMprODydKt4el3N3vG9gv2Am/RuZw==";
        const string ExpectedDeviceId =
            "ECERQOAXSQ24VDLBYHRJMXPVB57XHUCYHYYITCHZSQGG3KRJSA7Q";
        byte[] subjectPublicKeyInfo = Convert.FromBase64String(SubjectPublicKeyInfoBase64);

        DeviceId first = DeviceId.FromSubjectPublicKeyInfo(subjectPublicKeyInfo);
        DeviceId second = DeviceId.FromSubjectPublicKeyInfo(subjectPublicKeyInfo);

        Assert.Equal(first, second);
        Assert.Equal(ExpectedDeviceId, first.Value);
        Assert.Equal(DeviceId.EncodedLength, first.Value.Length);
        Assert.Matches("^[A-Z2-7]{52}$", first.Value);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => DeviceId.FromSubjectPublicKeyInfo(ReadOnlySpan<byte>.Empty));
        Assert.Equal("subjectPublicKeyInfo", exception.ParamName);
        Assert.StartsWith("The public key cannot be empty.", exception.Message, StringComparison.Ordinal);
    }
#pragma warning restore CA1707

    // This approved descriptive xUnit test name intentionally retains underscores.
#pragma warning disable CA1707
    [Fact]
    public void Invalid_device_id_text_is_rejected()
    {
        const string FormatMessage = "A device ID must be 52 uppercase Base32 characters.";
        const string UninitializedMessage = "An uninitialized device ID has no value.";
        DeviceId uninitialized = default;

        InvalidOperationException valueException = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = uninitialized.Value;
        });
        InvalidOperationException toStringException = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = uninitialized.ToString();
        });
        Assert.Equal(UninitializedMessage, valueException.Message);
        Assert.Equal(UninitializedMessage, toStringException.Message);

        Assert.Throws<ArgumentNullException>(() => DeviceId.Parse(null!));

        string canonicalTailA = new('A', DeviceId.EncodedLength);
        string canonicalTailQ = string.Concat(new string('A', DeviceId.EncodedLength - 1), "Q");
        DeviceId parsedTailA = DeviceId.Parse(canonicalTailA);
        DeviceId parsedTailQ = DeviceId.Parse(canonicalTailQ);
        Assert.Equal(canonicalTailA, parsedTailA.Value);
        Assert.Equal(canonicalTailA, parsedTailA.ToString());
        Assert.Equal(canonicalTailQ, parsedTailQ.Value);

        string lowercase = new('a', DeviceId.EncodedLength);
        string whitespace = string.Concat(new string('A', DeviceId.EncodedLength - 1), " ");
        string tooShort = new('A', DeviceId.EncodedLength - 1);
        string tooLong = new('A', DeviceId.EncodedLength + 1);
        string illegalCharacter = string.Concat(new string('A', DeviceId.EncodedLength - 1), "0");
        string noncanonicalTailB = string.Concat(new string('A', DeviceId.EncodedLength - 1), "B");
        string noncanonicalTail7 = string.Concat(new string('A', DeviceId.EncodedLength - 1), "7");
        string[] invalidValues =
        [
            "GD-invalid",
            lowercase,
            whitespace,
            tooShort,
            tooLong,
            illegalCharacter,
            noncanonicalTailB,
            noncanonicalTail7,
        ];

        foreach (string invalidValue in invalidValues)
        {
            FormatException exception = Assert.Throws<FormatException>(() => DeviceId.Parse(invalidValue));
            Assert.Equal(FormatMessage, exception.Message);
        }
    }
#pragma warning restore CA1707
}
