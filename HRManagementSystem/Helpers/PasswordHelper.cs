using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HR_KD.Helpers;

public static class PasswordHelper
{
    private const string TemporaryPrefix = "temporary:";

    public static string GenerateRandomKey(int length = 16)
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(length / 2 + length % 2))[..length];

    public static string CreateTemporaryPassword() => GenerateRandomKey(32);

    public static string HashTemporaryPassword(string password, string salt)
        => $"{TemporaryPrefix}{DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds()}:{BCrypt.Net.BCrypt.HashPassword(password + salt)}";

    public static bool IsTemporary(string hash) => hash.StartsWith(TemporaryPrefix, StringComparison.Ordinal);

    public static bool VerifyTemporary(string password, string salt, string hash)
    {
        if (!IsTemporary(hash))
            return false;

        var parts = hash.Split(':', 3);
        return parts.Length == 3
            && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expiry)
            && expiry > DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            && Verify(password, salt, parts[2]);
    }

    public static bool Verify(string password, string salt, string hash)
        => Encoding.UTF8.GetByteCount(password + salt) <= 72
            && BCrypt.Net.BCrypt.Verify(password + salt, hash);

    public static string SessionStamp(string hash)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash)));
}
