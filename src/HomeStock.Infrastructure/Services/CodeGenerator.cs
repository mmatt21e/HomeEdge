using System.Security.Cryptography;
using HomeStock.Application.Abstractions;

namespace HomeStock.Infrastructure.Services;

/// <summary>
/// Generates short, unambiguous, URL-safe codes for QR labels. Uses a Crockford-style base32
/// alphabet (no I/L/O/U) to avoid transcription errors, seeded from a cryptographic RNG.
/// </summary>
public class CodeGenerator : ICodeGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public string NewLocationCode() => "LOC-" + Random(6);
    public string NewItemLabelCode() => "ITM-" + Random(6);

    private static string Random(int length)
    {
        Span<byte> bytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(bytes);
        Span<char> chars = stackalloc char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        return new string(chars);
    }
}
