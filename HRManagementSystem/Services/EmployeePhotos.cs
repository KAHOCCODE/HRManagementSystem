namespace HR_KD.Services;

internal static class EmployeePhotos
{
    internal const string DefaultUrl = "/images/employees/Avatar.jpg";

    internal static string DisplayUrl(string? url) => string.IsNullOrWhiteSpace(url) ? DefaultUrl : url;

    public static bool IsValid(IFormFile photo)
    {
        if (photo.Length is < 12 or > (5 * 1024 * 1024))
            return false;

        Span<byte> header = stackalloc byte[12];
        using var stream = photo.OpenReadStream();
        stream.ReadExactly(header);
        return Path.GetExtension(photo.FileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => header[..3].SequenceEqual(new byte[] { 0xff, 0xd8, 0xff }),
            ".png" => header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".webp" => header[..4].SequenceEqual("RIFF"u8) && header[8..].SequenceEqual("WEBP"u8),
            _ => false
        };
    }

    public static string Save(IFormFile photo, int employeeId)
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "avatars");
        Directory.CreateDirectory(directory);
        var name = $"{employeeId}_{Guid.NewGuid():N}{Path.GetExtension(photo.FileName).ToLowerInvariant()}";
        using var stream = File.Create(Path.Combine(directory, name));
        photo.CopyTo(stream);
        return "/avatars/" + name;
    }

    public static void DeleteNewUpload(string? url)
    {
        if (url?.StartsWith("/avatars/", StringComparison.Ordinal) == true)
            File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "avatars", Path.GetFileName(url)));
    }
}
