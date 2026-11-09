using System.Security.Cryptography;

namespace EGWNCentral.Infrastructure.Messaging;

public static class RegistrationCodeGenerator
{
    public const string Alphabet = "ABCDEFGHIJKLMNPQRSTUVWXYZ123456789";
    public const int Length = 6;

    public static string Generate()
        => string.Create(Length, Alphabet, (characters, alphabet) =>
        {
            for (var index = 0; index < characters.Length; index++)
            {
                characters[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }
        });

    public static bool IsValid(string code)
        => code.Length == Length && code.All(character => Alphabet.Contains(character));
}
