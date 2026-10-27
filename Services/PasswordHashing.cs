using Microsoft.AspNetCore.Identity;
using StudentManagement.Models;

namespace StudentManagement.Services;

internal static class PasswordHashing
{
    private static readonly PasswordHasher<User> LegacyHasher = new();

    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public static bool Verify(User user, string password)
    {
        if (IsBcryptHash(user.PasswordHash))
            return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

        return LegacyHasher.VerifyHashedPassword(user, user.PasswordHash, password)
            != PasswordVerificationResult.Failed;
    }

    public static bool IsBcryptHash(string passwordHash) =>
        passwordHash.StartsWith("$2a$", StringComparison.Ordinal) ||
        passwordHash.StartsWith("$2b$", StringComparison.Ordinal) ||
        passwordHash.StartsWith("$2y$", StringComparison.Ordinal);
}
