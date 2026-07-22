namespace GoDesk.Core.Identity;

public static class Base32NoPadding
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        int encodedLength = checked(
            ((bytes.Length / 5) * 8) + ((((bytes.Length % 5) * 8) + 4) / 5));
        char[] encoded = new char[encodedLength];
        int encodedIndex = 0;
        int bitBuffer = 0;
        int bufferedBitCount = 0;

        foreach (byte value in bytes)
        {
            bitBuffer = (bitBuffer << 8) | value;
            bufferedBitCount += 8;

            while (bufferedBitCount >= 5)
            {
                bufferedBitCount -= 5;
                encoded[encodedIndex++] = Alphabet[(bitBuffer >> bufferedBitCount) & 0x1F];
            }

            bitBuffer &= (1 << bufferedBitCount) - 1;
        }

        if (bufferedBitCount > 0)
        {
            encoded[encodedIndex] = Alphabet[(bitBuffer << (5 - bufferedBitCount)) & 0x1F];
        }

        return new string(encoded);
    }
}
