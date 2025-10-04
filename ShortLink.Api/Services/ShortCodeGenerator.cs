using System.Security.Cryptography;

namespace ShortLink.Api.Services;

// Random Base62 rather than an encoded auto-increment id: sequential codes would let
// anyone walk the whole table by counting up from "1". 7 characters gives 62^7 (~3.5e12)
// possibilities, so collisions stay rare enough to handle with a retry at insert time.
public class ShortCodeGenerator : IShortCodeGenerator
{
    public const int CodeLength = 7;

    private const string Alphabet =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public string Generate()
    {
        return string.Create(CodeLength, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                // GetInt32 rejects biased samples internally, unlike Random.Next on a
                // range that doesn't divide evenly into the generator's output.
                span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }
        });
    }
}
